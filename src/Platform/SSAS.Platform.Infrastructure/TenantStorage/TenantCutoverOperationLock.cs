using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// THE ONE COORDINATION BOUNDARY FOR A CUTOVER OPERATION (ADR-020, TS-Storage Phase E3).
//
// It closes two different races on the same resource, which is why it is one lock rather than two:
//
//   1. TWO INSTANCES COPYING THE SAME CUTOVER. Both would read "Frozen", both would find the target empty,
//      and both would bulk-insert the same rows — producing a target that fails validation, or worse,
//      passes a count check while holding duplicates.
//
//   2. A RELEASE RACING A RUNNING COPY. E1's ReleaseFreeze deliberately took no lock, because release must
//      work when the source database is unhealthy. Once a copy exists that is no longer sufficient:
//      release → source writable → writers resume → the copy carries on reading a source that is moving
//      underneath it, and validates a target against data that changed after it was read. Release therefore
//      contends for this resource too, and a release that cannot take it is refused rather than delayed.
//
// IT LIVES IN THE PLATFORM DATABASE, because that is where the operation row lives and where both
// participants are already connected. The E1 write fence uses a DIFFERENT, tenant-scoped resource in the
// SOURCE database — the two are unrelated locks solving unrelated problems, and naming them apart is what
// keeps that obvious.
//
// OWNERSHIP IS TRANSIENT BY CONSTRUCTION. The copy holds it at Session scope on a dedicated connection, so
// a dead process releases it when its connection drops — no lease, no heartbeat, no stale-owner cleanup to
// get wrong. A retry then re-reads the durable source and target state and continues from what it finds.
internal static class TenantCutoverOperationLock
{
  public const string Prefix = "SSAS.TenantStorage.CutoverOperation.";

  public static string ForOperation(long cutoverOperationId) => string.Create(
    CultureInfo.InvariantCulture, $"{Prefix}{cutoverOperationId}");

  // Held for as long as the connection lives. Used by the copy, which must own the operation across many
  // transactions and cannot hold one open for the whole run.
  public static Task<bool> TryAcquireForSessionAsync(
    SqlConnection connection,
    long cutoverOperationId,
    TimeSpan timeout,
    CancellationToken cancellationToken = default) =>
    TryAcquireAsync(connection, null, cutoverOperationId, "Session", timeout, cancellationToken);

  // THE SAME ACQUISITION, RETURNING PROOF OF IT (TS-Storage Phase E5).
  //
  // The token exists so an "under ownership" entry point can REQUIRE ownership in its signature rather than
  // trusting a caller to have taken it. It cannot be constructed outside this type, so the only way to hold
  // one is to have actually won the lock — which is what stops the orchestrator's inner calls from either
  // re-acquiring (the E4 self-deadlock) or proceeding without ownership at all.
  public static async Task<TenantCutoverOwnership?> AcquireForSessionAsync(
    SqlConnection connection,
    long cutoverOperationId,
    TimeSpan timeout,
    CancellationToken cancellationToken = default) =>
    await TryAcquireForSessionAsync(connection, cutoverOperationId, timeout, cancellationToken)
      ? TenantCutoverOwnership.Grant(cutoverOperationId)
      : null;

  // Released by the caller's commit or rollback. Used by release, whose whole unit of work is one short
  // transaction.
  public static Task<bool> TryAcquireForTransactionAsync(
    SqlConnection connection,
    SqlTransaction transaction,
    long cutoverOperationId,
    TimeSpan timeout,
    CancellationToken cancellationToken = default) =>
    TryAcquireAsync(connection, transaction, cutoverOperationId, "Transaction", timeout, cancellationToken);

  private static async Task<bool> TryAcquireAsync(
    SqlConnection connection,
    SqlTransaction? transaction,
    long cutoverOperationId,
    string owner,
    TimeSpan timeout,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connection);

    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;
    command.Parameters.AddWithValue("@Resource", ForOperation(cutoverOperationId));
    command.Parameters.AddWithValue("@LockMode", "Exclusive");
    command.Parameters.AddWithValue("@LockOwner", owner);
    command.Parameters.AddWithValue("@LockTimeout", (int)timeout.TotalMilliseconds);

    var result = command.Parameters.Add("@Result", SqlDbType.Int);
    result.Direction = ParameterDirection.ReturnValue;
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;

    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 granted, 1 granted after waiting. Anything else — timeout, deadlock, cancel — means we do not own
    // the operation, and a participant that does not own it does not proceed.
    return Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;
  }
}

// PROOF THAT THE HOLDER OWNS A CUTOVER OPERATION (ADR-020, TS-Storage Phase E5).
//
// NOT FORGEABLE: the only constructor is private and the only factory is called by the lock helper after a
// successful acquisition, so a method that takes one in its signature genuinely cannot be entered without
// ownership. That turns "the caller must already hold the lock" from a comment into a compile-time demand.
//
// It carries no connection and no disposal responsibility. Ownership lives and dies with the SESSION that
// took it — the orchestrator's own connection — so this is evidence, not a resource; releasing is closing
// that connection, which a dying process does for free.
internal sealed class TenantCutoverOwnership
{
  private TenantCutoverOwnership(long cutoverOperationId) => CutoverOperationId = cutoverOperationId;

  public long CutoverOperationId { get; }

  internal static TenantCutoverOwnership Grant(long cutoverOperationId) => new(cutoverOperationId);
}
