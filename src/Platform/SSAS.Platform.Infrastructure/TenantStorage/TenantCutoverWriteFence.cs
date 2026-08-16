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
    if (await operations.RefusesApplicationWritesAsync(tenantId, cancellationToken))
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(
        TenantStorageErrors.TenantWritesFrozen);
    }
  }

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
public interface ITenantWriteFence
{
  Task AdmitWriteAsync(
    Guid tenantId,
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
