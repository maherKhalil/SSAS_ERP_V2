using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// THE TENANT WRITE FENCE (ADR-020 "Write consistency").
//
// Enforced at the tenant persistence boundary, NOT at the transport layer. ADR-020 is explicit about why: a
// request-path-only freeze would leave every non-HTTP writer — jobs, consumers, imports, workflows —
// running against the source during the copy, and everything they wrote after the copy passed their table
// would be lost silently and invisibly to validation.
//
// TWO MECHANISMS, CLOSING TWO DIFFERENT HOLES, and neither is sufficient alone:
//
//   1. A SHARED, TRANSACTION-OWNED APPLICATION LOCK, taken on the tenant's own connection inside the write
//      transaction. The freezer takes the same resource EXCLUSIVELY, so it cannot proceed until every
//      in-flight writer's transaction has ended, and no new writer can enter while it holds it. This is
//      what closes the race a boolean cannot: check "not frozen" → freezer freezes → old writer commits.
//
//   2. A DURABLE READ of the cutover row, performed AFTER the lock is held. The lock drains; the row
//      decides. A lock alone would release the moment a cutover process died, silently readmitting writes
//      to a tenant whose data is half-copied.
//
// THE ORDER IS THE WHOLE DESIGN. Lock first, then read. Reading first would leave exactly the gap the lock
// exists to close, because the freezer could commit between the read and the write.
//
// THE RESOURCE IS TENANT-SCOPED. A shared database hosts many tenants and one tenant's promotion is not an
// outage for its co-tenants (ADR-020 "Freeze scope"), so the lock name carries the TenantId and there is no
// database-wide or fleet-wide resource anywhere in this path.
public sealed class TenantCutoverWriteFence(
  ITenantCutoverOperationStore operations,
  IOptions<TenantCutoverFreezeOptions> optionsAccessor) : ITenantWriteFence
{
  public async Task AdmitWriteAsync(
    Guid tenantId,
    long tenantDatabaseId,
    DbConnection connection,
    DbTransaction transaction,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentNullException.ThrowIfNull(transaction);

    var options = optionsAccessor.Value;

    // A BOUNDED WAIT. While a freeze is draining, writers queue here — and a request must fail visibly
    // rather than hang for the whole cutover window, so exceeding the budget is refused as frozen.
    var acquired = await TryAcquireSharedAsync(
      tenantId, connection, transaction, options.WriteAdmissionTimeout, cancellationToken);
    if (!acquired)
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(
        TenantStorageErrors.TenantWritesFrozen);
    }

    // ...and only now is the durable answer meaningful.
    var gate = await operations.FindActiveWriteGateAsync(tenantId, cancellationToken);
    if (gate is null)
    {
      return;
    }

    // ---- THE COPY WINDOW. Nothing writes anywhere: the copy is reading a source that must not move, and
    // the target is not the tenant's database yet.
    if (gate.RefusesEveryWrite)
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(
        TenantStorageErrors.TenantWritesFrozen);
    }

    // ---- AFTER THE FLIP THE ANSWER DEPENDS ON WHICH DATABASE THIS WRITER IS BOUND TO (E4).
    //
    // A context created BEFORE the flip still holds a connection to the source, and its RoutingVersion is
    // stale — but it does not re-resolve, so E2's version check never runs for it. This is the only place
    // that in-flight writer is caught, and catching it is the whole point of the fence being route-aware
    // rather than tenant-aware: refusing by TenantId alone would also refuse the target, freezing the
    // tenant permanently, while permitting by TenantId alone would let the stale context write into the
    // database the tenant was just moved off.
    if (!gate.PermitsWriteTo(tenantDatabaseId))
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(
        TenantStorageErrors.TenantWritesFrozen);
    }

    // ---- THE FIRST WRITE ONTO THE NEW DATABASE, recorded because it decides which rollback regime is
    // available at all (ADR-020).
    //
    // RECORDED AT ADMISSION, WHICH IS DELIBERATELY CONSERVATIVE. The tenant write commits in the TENANT
    // database and this observation lives in the PLATFORM database, so they cannot share a transaction; if
    // the tenant write later rolls back, this will have recorded a write that did not land. That direction
    // is the safe one — it can only make the platform MORE reluctant to consider a flipback, never less —
    // whereas recording after commit could miss a write that did land, which is the failure that matters.
    if (gate.IsFirstTargetWrite)
    {
      await operations.RecordPostCutoverWriteAsync(gate.CutoverOperationId, PostCutoverActor, cancellationToken);
    }
  }

  private const string PostCutoverActor = "tenant-cutover-post-write";

  // `sys.sp_getapplock` at Shared mode, owned by the TRANSACTION so it is released by commit, rollback, or
  // the connection dying — never left behind by a crashed writer.
  private static async Task<bool> TryAcquireSharedAsync(
    Guid tenantId,
    DbConnection connection,
    DbTransaction transaction,
    TimeSpan timeout,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;

    AddParameter(command, "@Resource", TenantCutoverLockResource.ForTenant(tenantId));
    AddParameter(command, "@LockMode", "Shared");
    AddParameter(command, "@LockOwner", "Transaction");
    AddParameter(command, "@LockTimeout", (int)timeout.TotalMilliseconds);

    var result = command.CreateParameter();
    result.ParameterName = "@Result";
    result.DbType = DbType.Int32;
    result.Direction = ParameterDirection.ReturnValue;
    command.Parameters.Add(result);

    // The command must outlive the lock wait, or a transport timeout would surface instead of the clean
    // negative return sp_getapplock exists to give.
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;
    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 granted, 1 granted after waiting. Everything else — timeout, deadlock, cancel — means we do not
    // hold it, and a writer that does not hold the fence does not write.
    return Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;
  }

  private static void AddParameter(DbCommand command, string name, object value)
  {
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
  }
}

// The boundary the tenant persistence context consults before any application write commits.
//
// IT TAKES THE PHYSICAL DATABASE AS WELL AS THE TENANT (TS-Storage Phase E4). Once a cutover can move a
// tenant between databases, "may this tenant write?" has no single answer: the same tenant is refused on
// the database it left and permitted on the one it moved to, and only the caller's route knows which one
// this write is bound for.
public interface ITenantWriteFence
{
  Task AdmitWriteAsync(
    Guid tenantId,
    long tenantDatabaseId,
    DbConnection connection,
    DbTransaction transaction,
    CancellationToken cancellationToken = default);
}

// One place that knows the lock's name, so the writer and the freezer cannot drift onto different
// resources — which would look exactly like a working fence and protect nothing.
public static class TenantCutoverLockResource
{
  public const string Prefix = "SSAS.TenantStorage.Cutover.";

  public static string ForTenant(Guid tenantId) => string.Create(
    CultureInfo.InvariantCulture, $"{Prefix}{tenantId:D}");
}
