using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Server-visibility checks for backup in-flight detection (ADR-022 §14).
//
// EXTRACTED AS ITS OWN UNIT so the permission boundary can be exercised by a test running under a genuinely
// low-privilege principal. The earlier arrangement kept this SQL private to the provider, which meant the
// only way to test it was to re-implement it — and a re-implementation cannot catch the defect below.
//
// THE DEFECT THIS EXISTS TO PREVENT:
//
// `sys.dm_exec_requests` does NOT raise an error when the caller lacks server-wide visibility. It silently
// returns ONLY THE CALLER'S OWN SESSION. Microsoft's documentation states it plainly: "If the user has VIEW
// SERVER STATE permission on the server, the user sees all executing sessions on the instance of SQL Server;
// otherwise, the user sees only the current session" — and, for SQL Server 2022 and later, the permission
// that grants it is VIEW SERVER PERFORMANCE STATE.
//
// So a guard that only catches exceptions fails OPEN: a deployment that grants db_backupoperator but forgets
// the server-level permission reads "no rows" as "nothing in flight" and starts a backup anyway. That is the
// exact inversion of what the guard is for, and it is silent.
//
// The permission is therefore established EXPLICITLY, before the DMV result is trusted at all.
internal static class SqlServerBackupVisibility
{
  // The granular SQL Server 2022 permission for sys.dm_exec_requests. Deliberately NOT VIEW SERVER STATE
  // (broader than needed) and emphatically not sysadmin. Nothing here grants it — deployment does.
  public const string RequiredPermission = "VIEW SERVER PERFORMANCE STATE";

  // Can this identity see beyond its own session?
  //
  // HAS_PERMS_BY_NAME(NULL, NULL, ...) asks the question at SERVER scope. It returns 1 when held, 0 when
  // not, and NULL for an unrecognised permission name — and NULL is treated as "no", because an answer we
  // cannot interpret is not permission to proceed.
  public static async Task<bool> HasInFlightVisibilityAsync(
    SqlConnection connection,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);

    try
    {
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT HAS_PERMS_BY_NAME(NULL, NULL, @permission)";
      command.Parameters.Add("@permission", System.Data.SqlDbType.NVarChar, 128).Value = RequiredPermission;

      var value = await command.ExecuteScalarAsync(cancellationToken);
      return value is not null and not DBNull && Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }
    catch (SqlException)
    {
      // Could not even ask. Same answer as "does not hold it".
      return false;
    }
  }

  // Whether a server-side backup is already running against the CURRENT database.
  //
  // Only meaningful once HasInFlightVisibilityAsync has returned true. The caller must not invert that order:
  // this method cannot distinguish "nothing running" from "not allowed to see what is running", which is
  // precisely why the permission is established first.
  public static async Task<bool> IsBackupInFlightAsync(
    SqlConnection connection,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);

    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT TOP (1) 1 FROM sys.dm_exec_requests " +
      "WHERE database_id = DB_ID() AND command LIKE 'BACKUP%' AND session_id <> @@SPID";

    var found = await command.ExecuteScalarAsync(cancellationToken);
    return found is not null and not DBNull;
  }
}
