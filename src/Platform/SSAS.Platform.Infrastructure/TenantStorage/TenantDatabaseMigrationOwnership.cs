using System.Data;
using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Single-writer ownership of one physical tenant database during migration (ADR-018).
//
// Implemented as a SQL Server application lock (`sp_getapplock`) taken IN THE TARGET DATABASE, at Session
// scope. That choice satisfies the eight binding lock invariants, and each one is worth naming because
// they are what make a fleet migration safe:
//
//  1. Mutual exclusion is scoped to ONE database — the lock lives in that database, so a slow database
//     cannot serialise the estate.
//  2. Ownership is acquired before any DDL — the orchestrator acquires before it begins.
//  3. Held through post-verification — the lock is released by disposing this handle, which the
//     orchestrator does only after re-reading the resulting history.
//  4. Bounded acquisition — sp_getapplock takes an explicit timeout and returns -1 rather than blocking.
//  5. Failure to acquire is a clean skip — a -1 becomes SkippedOwnershipHeld, never a forced proceed.
//  6. Loss mid-run aborts — the connection carrying the lock is the connection running the migration, so
//     losing one means losing the other; the run cannot continue believing it still holds ownership.
//  7. Crash-safe — Session-scoped locks are released by SQL Server when the session ends, so a crashed
//     holder leaves neither a permanent lock nor a second writer. This is the decisive advantage over a
//     lease row, which would need its own expiry, clock assumptions and reaper.
//  8. Post-verification under the same ownership — same connection, same lock, no window between apply
//     and verify.
public sealed class TenantDatabaseMigrationOwnership : IAsyncDisposable
{
  // One lock name per physical database. The database itself provides the scope, so the resource name
  // only needs to identify the concern.
  private const string LockResource = "SSAS.TenantStorage.Migration";

  private const string LockOwner = "Session";

  private readonly SqlConnection connection;
  private bool released;

  private TenantDatabaseMigrationOwnership(SqlConnection connection) => this.connection = connection;

  // Attempts to take ownership on the supplied OPEN connection. Returns null when the lock is already
  // held elsewhere — the caller reports a skip rather than proceeding.
  public static async Task<TenantDatabaseMigrationOwnership?> TryAcquireAsync(
    SqlConnection connection,
    TimeSpan timeout,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);

    await using var command = connection.CreateCommand();
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;
    command.Parameters.AddWithValue("@Resource", LockResource);
    command.Parameters.AddWithValue("@LockMode", "Exclusive");
    command.Parameters.AddWithValue("@LockOwner", LockOwner);
    command.Parameters.AddWithValue("@LockTimeout", (int)timeout.TotalMilliseconds);

    var returnValue = command.Parameters.Add("@Result", SqlDbType.Int);
    returnValue.Direction = ParameterDirection.ReturnValue;

    // The command must not time out before the lock wait does, or the caller would see a transport error
    // instead of the clean "held elsewhere" answer sp_getapplock is there to give.
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;
    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 = granted, 1 = granted after waiting. Negative values are timeout (-1), deadlock (-2), cancel
    // (-3) or a parameter error (-999); all are "we do not own it".
    var result = (int)returnValue.Value;
    return result is 0 or 1 ? new TenantDatabaseMigrationOwnership(connection) : null;
  }

  // Verifies ownership is still held. Used before post-verification so a lock lost mid-run aborts rather
  // than being treated as probably-fine (invariant 6).
  public async Task<bool> IsStillHeldAsync(CancellationToken cancellationToken = default)
  {
    if (released || connection.State != ConnectionState.Open)
    {
      return false;
    }

    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT APPLOCK_MODE(N'public', @resource, @owner)";
    command.Parameters.AddWithValue("@resource", LockResource);
    command.Parameters.AddWithValue("@owner", LockOwner);

    var mode = Convert.ToString(
      await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    return string.Equals(mode, "Exclusive", StringComparison.Ordinal);
  }

  public async ValueTask DisposeAsync()
  {
    if (released)
    {
      return;
    }

    released = true;

    // Best-effort explicit release. If the connection has already failed, SQL Server releases the
    // session-scoped lock when the session ends, which is invariant 7 doing its job.
    if (connection.State != ConnectionState.Open)
    {
      return;
    }

    try
    {
      await using var command = connection.CreateCommand();
      command.CommandText = "sys.sp_releaseapplock";
      command.CommandType = CommandType.StoredProcedure;
      command.Parameters.AddWithValue("@Resource", LockResource);
      command.Parameters.AddWithValue("@LockOwner", LockOwner);
      await command.ExecuteNonQueryAsync();
    }
    catch (SqlException)
    {
      // Releasing a lock we no longer hold is not an error worth propagating from a dispose path.
    }
  }
}
