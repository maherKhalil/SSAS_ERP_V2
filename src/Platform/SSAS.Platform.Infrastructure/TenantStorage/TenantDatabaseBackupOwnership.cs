using System.Data;
using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Single-writer ownership of one physical tenant database during a managed backup (ADR-022 §14).
//
// A deliberate sibling of TenantDatabaseMigrationOwnership rather than a shared generic: the two protect
// DIFFERENT concerns and must NOT contend with each other. ADR-022 is explicit that the migration and backup
// resources are deliberately non-contending — migration ownership prevents a second migration, backup
// ownership prevents a second platform-managed backup, and neither is intended to exclude the other. All
// managed backup types may run during a migration.
//
// The resource name is a CONSTANT, following the implemented migration precedent: the lock is taken IN THE
// TARGET DATABASE, so the database itself already scopes it and embedding the database identity in the name
// would be redundant. What ADR-022 §14 requires — a namespace distinct from migration's — is satisfied by
// the distinct constant.
public sealed class TenantDatabaseBackupOwnership : IAsyncDisposable
{
  // Distinct from "SSAS.TenantStorage.Migration" by design. These two must never collide, and must never
  // exclude one another.
  private const string LockResource = "SSAS.TenantStorage.Backup";

  private const string LockOwner = "Session";

  private readonly SqlConnection connection;
  private bool released;

  private TenantDatabaseBackupOwnership(SqlConnection connection) => this.connection = connection;

  // Takes ownership on the supplied OPEN connection — which must be the SAME connection that will issue the
  // BACKUP. Session-scoped ownership on a different connection would be meaningless: the lock would outlive
  // a dead backup session, or die while the backup continued.
  //
  // Returns null when the lock is held elsewhere, so the caller reports a clean skip rather than proceeding.
  public static async Task<TenantDatabaseBackupOwnership?> TryAcquireAsync(
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

    // The command must not time out before the lock wait does, or the caller sees a transport error instead
    // of the clean "held elsewhere" answer sp_getapplock exists to give.
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;
    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 = granted, 1 = granted after waiting. Negatives are timeout (-1), deadlock (-2), cancel (-3) or a
    // parameter error (-999); every one of them means "we do not own it".
    var result = (int)returnValue.Value;
    return result is 0 or 1 ? new TenantDatabaseBackupOwnership(connection) : null;
  }

  // Confirms ownership is STILL held. Called before a run may be recorded successful: ADR-022 §14 forbids
  // recording success after session ownership is lost, however the command itself appeared to end.
  public async Task<bool> IsStillHeldAsync(CancellationToken cancellationToken = default)
  {
    if (released || connection.State != ConnectionState.Open)
    {
      return false;
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT APPLOCK_MODE(N'public', @resource, @owner)";
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

    // Best-effort explicit release. Safety does NOT depend on this succeeding: the connection is non-pooled,
    // so closing it genuinely ends the session, and SQL Server releases session-scoped locks when a session
    // ends. That is the property this whole mechanism rests on.
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
      // Releasing a lock we no longer hold is not worth propagating from a dispose path.
    }
  }
}
