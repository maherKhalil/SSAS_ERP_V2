using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

public interface ITenantCutoverFreezeService
{
  Task<Result<long>> BeginAsync(
    TenantCutoverBeginRequest request,
    CancellationToken cancellationToken = default);

  // Drains in-flight tenant writes and makes the freeze durable. Either both happen or neither does.
  Task<Result> FreezeAsync(long cutoverOperationId, CancellationToken cancellationToken = default);

  Task<Result> ReleaseFreezeAsync(
    long cutoverOperationId,
    string? failureSummary = null,
    CancellationToken cancellationToken = default);
}

// Establishes and releases the tenant write freeze (ADR-020 "Write consistency", TS-Storage Phase E1).
//
// THE SEQUENCE IS THE CORRECTNESS ARGUMENT:
//
//   1. record that a drain was requested          — so a crash mid-drain is distinguishable from no attempt
//   2. take the tenant's EXCLUSIVE application lock on the SOURCE database, with a bounded timeout
//        → this blocks until every in-flight writer's shared lock is gone: the drain
//        → and holds new writers out for as long as we keep it
//   3. WHILE STILL HOLDING IT, commit the durable Frozen row
//   4. only then release the lock
//
// Step 3 before step 4 is not an implementation detail. Releasing first would open a window in which the
// drain had finished, the durable fence was not yet visible, and a writer could commit against a source
// the copy was about to read — which is the precise failure the freeze exists to prevent.
//
// NOTHING HERE COPIES, VALIDATES OR FLIPS ROUTING. This slice establishes the window; it does not use it.
internal sealed class TenantCutoverFreezeService(
  ITenantCutoverOperationStore operations,
  ITenantDatabaseResolver resolver,
  ITenantDatabaseConnectionFactory connectionFactory,
  IOptions<TenantCutoverFreezeOptions> optionsAccessor) : ITenantCutoverFreezeService
{
  private const string Actor = "tenant-cutover-freeze";

  public Task<Result<long>> BeginAsync(
    TenantCutoverBeginRequest request,
    CancellationToken cancellationToken = default) =>
    operations.BeginAsync(request, cancellationToken);

  public async Task<Result> FreezeAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default)
  {
    var operation = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (operation is null)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotFound);
    }

    // Idempotent. A resumed cutover that finds its freeze already durable has nothing to drain.
    if (operation.Status == TenantCutoverOperationStatus.Frozen)
    {
      return Result.Success();
    }

    if (operation.Status != TenantCutoverOperationStatus.Preparing)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotPreparing);
    }

    var requested = await operations.RequestFreezeAsync(cutoverOperationId, Actor, cancellationToken);
    if (requested.IsFailure)
    {
      return requested;
    }

    // The lock lives in the SOURCE database — the one the writers are connected to — so the freezer and the
    // writers contend on the same resource. Resolving the route rather than accepting a connection also
    // re-establishes that the tenant still routes where the operation says it does.
    var route = await resolver.ResolveAsync(operation.TenantId, cancellationToken);
    if (route.IsFailure)
    {
      await FailAsync(cutoverOperationId, route.Error.Code, cancellationToken);
      return Result.Failure(route.Error);
    }

    if (route.Value.TenantDatabaseId != operation.SourceTenantDatabaseId)
    {
      await FailAsync(
        cutoverOperationId, TenantStorageErrors.CutoverSourceNotEligible.Code, cancellationToken);
      return Result.Failure(TenantStorageErrors.CutoverSourceNotEligible);
    }

    var connection = connectionFactory.Create(route.Value);
    if (connection.IsFailure)
    {
      await FailAsync(cutoverOperationId, connection.Error.Code, cancellationToken);
      return Result.Failure(connection.Error);
    }

    await using var sqlConnection = connection.Value;
    await sqlConnection.OpenAsync(cancellationToken);

    // Transaction-owned, so the lock is released by the commit below and by connection loss — never
    // stranded. The DURABLE row, not this lock, is what keeps writes refused afterwards.
    await using var drain = (SqlTransaction)await sqlConnection.BeginTransactionAsync(cancellationToken);

    var drained = await TryDrainAsync(
      operation.TenantId, sqlConnection, drain,
      optionsAccessor.Value.DrainTimeout, cancellationToken);
    if (!drained)
    {
      await drain.RollbackAsync(CancellationToken.None);
      await FailAsync(
        cutoverOperationId, TenantStorageErrors.CutoverFreezeTimedOut.Code, cancellationToken);
      return Result.Failure(TenantStorageErrors.CutoverFreezeTimedOut);
    }

    // ---- STILL HOLDING THE DRAIN LOCK. Nothing can write between here and the row becoming visible.
    var frozen = await operations.FreezeAsync(cutoverOperationId, Actor, cancellationToken);
    if (frozen.IsFailure)
    {
      await drain.RollbackAsync(CancellationToken.None);
      return frozen;
    }

    await drain.CommitAsync(cancellationToken);
    return Result.Success();
  }

  public async Task<Result> ReleaseFreezeAsync(
    long cutoverOperationId,
    string? failureSummary = null,
    CancellationToken cancellationToken = default)
  {
    // NO LOCK IS INVOLVED. Release makes the durable row stop refusing writes; the drain lock was handed
    // back when the freeze transaction committed. Keeping release lock-free is deliberate — it is the one
    // step that must succeed even when the source database is unhealthy, because a tenant must never be
    // left permanently frozen (ADR-020 "Freeze failure safety").
    return await operations.ReleaseFreezeAsync(
      cutoverOperationId, failureSummary, Actor, cancellationToken);
  }

  private async Task FailAsync(long cutoverOperationId, string reason, CancellationToken cancellationToken) =>
    await operations.FailFreezeAsync(cutoverOperationId, reason, Actor, cancellationToken);

  // The drain. Exclusive on the same tenant-scoped resource every writer takes at Shared, so acquisition
  // means "no tenant write transaction is open, and none can open".
  private static async Task<bool> TryDrainAsync(
    Guid tenantId,
    SqlConnection connection,
    SqlTransaction transaction,
    TimeSpan timeout,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;
    command.Parameters.AddWithValue("@Resource", TenantCutoverLockResource.ForTenant(tenantId));
    command.Parameters.AddWithValue("@LockMode", "Exclusive");
    command.Parameters.AddWithValue("@LockOwner", "Transaction");
    command.Parameters.AddWithValue("@LockTimeout", (int)timeout.TotalMilliseconds);

    var result = command.Parameters.Add("@Result", SqlDbType.Int);
    result.Direction = ParameterDirection.ReturnValue;
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;

    await command.ExecuteNonQueryAsync(cancellationToken);
    return Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;
  }
}

// Deployment configuration for the cutover freeze (ADR-020). NOTHING HERE IS A SAFETY CONTROL: there is no
// switch that skips the drain or bypasses the durable fence.
public sealed class TenantCutoverFreezeOptions
{
  public const string SectionName = "TenantStorage:Cutover";

  // How long the freezer waits for existing tenant writes to drain. Exceeding it FAILS the freeze — it
  // never downgrades to a partial claim. A measured budget per tenant size is an operational input
  // (ADR-020 "Freeze budget"); this is only the ceiling.
  public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

  // How long an ordinary application write waits at the fence while a drain is in progress. Deliberately
  // much shorter: a request should fail visibly rather than hang for the cutover window.
  public TimeSpan WriteAdmissionTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
