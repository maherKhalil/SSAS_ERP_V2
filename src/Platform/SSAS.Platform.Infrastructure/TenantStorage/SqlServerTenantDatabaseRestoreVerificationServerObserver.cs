using Microsoft.Data.SqlClient;
using System.Data;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Read-only D8 observation through the same trusted VerificationServers boundary as D6/D7. A failed
// connection or DMV read returns an explicitly unobserved result; it never becomes evidence that a restore
// or verification database is absent.
public sealed class SqlServerTenantDatabaseRestoreVerificationServerObserver(
  ITenantDatabaseVerificationConnectionFactory connectionFactory)
  : ITenantDatabaseRestoreVerificationServerObserver
{
  public async Task<TenantDatabaseRestoreVerificationServerObservation> ObserveAsync(
    TenantDatabaseRestoreVerificationServerObservationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!TenantDatabaseVerificationNaming.IsVerificationDatabaseName(request.VerificationDatabaseName))
    {
      return Unobserved(TenantStorageErrors.RestoreVerificationTargetNameNotSafe.Code);
    }

    var connection = connectionFactory.Create(
      new TenantDatabaseVerificationTarget(request.RestoreServerKey, request.SourceServerKey));
    if (connection.IsFailure)
    {
      return Unobserved(connection.Error.Code);
    }

    await using var sqlConnection = connection.Value;
    try
    {
      if (sqlConnection.State != ConnectionState.Open)
      {
        await sqlConnection.OpenAsync(cancellationToken);
      }

      // sys.dm_exec_requests silently narrows to the caller's session when this permission is absent. A
      // successful but filtered query is therefore unobservable, not authoritative evidence of absence.
      if (!await SqlServerBackupVisibility.HasInFlightVisibilityAsync(sqlConnection, cancellationToken))
      {
        return Unobserved("RestoreVerificationActivityVisibilityUnavailable");
      }

      await using var command = sqlConnection.CreateCommand();
      command.CommandTimeout = 30;
      command.CommandText = """
        SELECT
          database_state = (SELECT [state_desc] FROM sys.databases WHERE [name] = @verificationDatabaseName),
          restore_is_active = CONVERT(bit, CASE WHEN EXISTS
          (
            SELECT 1
            FROM sys.dm_exec_requests AS request
            CROSS APPLY sys.dm_exec_sql_text(request.[sql_handle]) AS statement
            WHERE request.[command] IN (N'RESTORE DATABASE', N'RESTORE LOG')
              AND CHARINDEX(QUOTENAME(@verificationDatabaseName), statement.[text]) > 0
          ) THEN 1 ELSE 0 END);
        """;
      command.Parameters.Add(new SqlParameter("@verificationDatabaseName", request.VerificationDatabaseName));

      await using var reader = await command.ExecuteReaderAsync(cancellationToken);
      if (!await reader.ReadAsync(cancellationToken))
      {
        return Unobserved("ObservationResultMissing");
      }

      var state = reader.IsDBNull(0) ? null : reader.GetString(0);
      return new TenantDatabaseRestoreVerificationServerObservation(
        ServerStateObserved: true,
        VerificationDatabaseExists: state is not null,
        RestoreIsActiveOnServer: reader.GetBoolean(1),
        VerificationDatabaseState: state);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      return Unobserved(exception.GetType().Name);
    }
  }

  private static TenantDatabaseRestoreVerificationServerObservation Unobserved(string reason) =>
    new(false, false, false, UnavailableReason: reason);
}
