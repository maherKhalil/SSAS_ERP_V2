using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The SQL Server backup provider (ADR-022 §9, §10, §14). The ONLY component in the system that issues a
// BACKUP statement.
//
// It owns exactly one operation against one physical database, and nothing about scheduling, policy or
// domain state. The sequence it guarantees is what makes a backup trustworthy rather than merely attempted:
//
//   1. open a NON-POOLED privileged connection to the target database
//   2. take SESSION-scoped ownership on THAT connection
//   3. optionally check for a server-side backup already in flight
//   4. validate database state, recovery model and chain preconditions
//   5. issue the backup on the SAME connection that holds the lock
//   6. re-confirm ownership is still held
//   7. RECONCILE msdb evidence by deterministic correlation
//
// Steps 6 and 7 are the ones that separate this from "the command returned". ADR-022 §14 is explicit that a
// completed ExecuteNonQuery is not evidence a usable backup exists.
public sealed class SqlServerTenantDatabaseBackupProvider(
  ITenantDatabaseRegistryReadRepository registry,
  ITenantDatabaseBackupConnectionFactory connectionFactory,
  IOptions<TenantStorageOptions> optionsAccessor,
  TenantDatabaseBackupOperationalOptions operationalOptions) : ITenantDatabaseBackupProvider
{
  private const int SafeErrorSummaryMaximumLength = TenantDatabaseBackupRun.ErrorSummaryMaximumLength;

  public async Task<TenantDatabaseBackupProviderResult> ExecuteAsync(
    TenantDatabaseBackupRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var descriptor = await FindDescriptorAsync(request.TenantDatabaseId, cancellationToken);
    if (descriptor is null)
    {
      return Blocked(TenantStorageErrors.TenantDatabaseRequired.Code);
    }

    // Destination resolution happens HERE, from a trusted key, entirely inside Infrastructure. Nothing above
    // this layer has seen or supplied a path (ADR-022 compliance rule 23).
    var resolver = new TenantDatabaseBackupDestinationResolver(optionsAccessor);
    var destination = resolver.Resolve(request.Options.DestinationKey);
    if (destination.IsFailure)
    {
      return Blocked(destination.Error.Code);
    }

    var connectionResult = connectionFactory.Create(new TenantDatabaseConnectionTarget(
      descriptor.ServerKey, descriptor.DatabaseName, descriptor.HostingMode));
    if (connectionResult.IsFailure)
    {
      return Blocked(connectionResult.Error.Code);
    }

    var startedUtc = DateTimeOffset.UtcNow;
    await using var connection = connectionResult.Value;

    try
    {
      await connection.OpenAsync(cancellationToken);

      // Ownership on THIS connection, before anything else touches the database.
      await using var ownership = await TenantDatabaseBackupOwnership.TryAcquireAsync(
        connection, operationalOptions.OwnershipTimeout, cancellationToken);
      if (ownership is null)
      {
        // Another platform worker owns this database's backups right now. Coordination worked.
        return new TenantDatabaseBackupProviderResult(
          TenantDatabaseBackupOutcome.SkippedOwnershipHeld, StartedUtc: startedUtc,
          CompletedUtc: DateTimeOffset.UtcNow);
      }

      // The in-flight guard (ADR-022 §14). Enabled by configuration because whether it is REQUIRED depends
      // on the empirical session-loss result: if session loss provably stops a server-side BACKUP before
      // another worker can acquire ownership, the applock alone suffices; if it does not, this closes the
      // window. It is cheap enough to leave on regardless.
      if (operationalOptions.InFlightDetectionEnabled &&
        await IsBackupInFlightAsync(connection, cancellationToken))
      {
        return new TenantDatabaseBackupProviderResult(
          TenantDatabaseBackupOutcome.SkippedInFlightOperation, StartedUtc: startedUtc,
          CompletedUtc: DateTimeOffset.UtcNow);
      }

      var precondition = await ValidatePreconditionsAsync(connection, request, cancellationToken);
      if (precondition is not null)
      {
        return Blocked(precondition, startedUtc);
      }

      var compression = SqlServerBackupCommandText.ResolveCompression(
        request.Options.CompressionMode,
        SqlServerBackupCommandText.SupportsCompression(await ReadEngineEditionAsync(connection, cancellationToken)));
      if (compression.IsFailure)
      {
        return Blocked(compression.Error.Code, startedUtc);
      }

      var fileName = SqlServerBackupCommandText.ArtifactFileName(
        request.TenantDatabaseId, request.Operation, request.BackupRunId, startedUtc);
      var fullPath = Path.Combine(destination.Value.DirectoryPath, fileName);

      await ExecuteBackupAsync(connection, request, descriptor.DatabaseName, fullPath, compression.Value, cancellationToken);

      // OWNERSHIP RE-CHECK before any success may be claimed. A lock lost mid-operation means this worker no
      // longer speaks for this database, whatever the command reported (ADR-022 §14).
      if (!await ownership.IsStillHeldAsync(cancellationToken))
      {
        return Failed(TenantStorageErrors.BackupOwnershipLost.Code, startedUtc);
      }

      // EVIDENCE RECONCILIATION. Correlated on the unique artifact path this run generated — never "the
      // latest backup for this database", which would happily adopt a DBA's ad-hoc backup as ours.
      var evidence = await ReadEvidenceAsync(connection, descriptor.DatabaseName, fileName, cancellationToken);
      if (evidence is null)
      {
        return Failed(TenantStorageErrors.BackupEvidenceMissing.Code, startedUtc);
      }

      return new TenantDatabaseBackupProviderResult(
        TenantDatabaseBackupOutcome.Succeeded,
        ProviderBackupSetIdentity: evidence.BackupSetIdentity,
        SafeArtifactReference: fileName,
        BackupSizeBytes: evidence.BackupSizeBytes,
        FirstLsn: evidence.FirstLsn,
        LastLsn: evidence.LastLsn,
        DatabaseBackupLsn: evidence.DatabaseBackupLsn,
        BackupSetGuid: evidence.BackupSetGuid,
        StartedUtc: startedUtc,
        CompletedUtc: evidence.FinishedUtc ?? DateTimeOffset.UtcNow);
    }
    catch (SqlException exception)
    {
      // Includes the destination-not-writable case (OS error 5): the SQL Server SERVICE identity writes the
      // file, not this process, so a destination the application can reach may still be refused.
      return Failed(Summarise(exception), startedUtc);
    }
    catch (InvalidOperationException exception)
    {
      return Failed(Summarise(exception), startedUtc);
    }
  }

  private async Task<TenantDatabaseDescriptor?> FindDescriptorAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken)
  {
    // Phase B targets ONE already-resolved database, so this is a bounded lookup rather than a fleet scan.
    var page = await registry.ListPhysicalDatabasesAsync(tenantDatabaseId - 1, 1, cancellationToken);
    var descriptor = page.Count > 0 ? page[0] : null;
    return descriptor?.TenantDatabaseId == tenantDatabaseId ? descriptor : null;
  }

  // Preconditions that make an operation impossible BEFORE it is issued, so there is no partial work to
  // reconcile afterwards.
  private static async Task<string?> ValidatePreconditionsAsync(
    SqlConnection connection,
    TenantDatabaseBackupRequest request,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Status') AS nvarchar(60)), " +
      "CAST(DATABASEPROPERTYEX(DB_NAME(), 'Recovery') AS nvarchar(60))";

    string? state = null;
    string? recoveryModel = null;
    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
    {
      if (await reader.ReadAsync(cancellationToken))
      {
        state = reader.IsDBNull(0) ? null : reader.GetString(0);
        recoveryModel = reader.IsDBNull(1) ? null : reader.GetString(1);
      }
    }

    // ONLINE only for V1. RESTORING, RECOVERY_PENDING, SUSPECT and OFFLINE are refused rather than attempted;
    // the provider classifies and reports rather than issuing commands blindly (ADR-022 §9).
    if (!string.Equals(state, "ONLINE", StringComparison.OrdinalIgnoreCase))
    {
      return TenantStorageErrors.BackupDatabaseStateUnsupported.Code;
    }

    if (request.Operation.OperationCode == "TransactionLog")
    {
      // FULL and BULK_LOGGED both support log backups; SIMPLE cannot produce one at all. The platform
      // reports this rather than switching the model, because switching starts log growth on a database that
      // is by definition misconfigured (ADR-022 §9).
      if (string.Equals(recoveryModel, "SIMPLE", StringComparison.OrdinalIgnoreCase))
      {
        return TenantStorageErrors.BackupRecoveryModelUnsupported.Code;
      }

      // A FULL recovery model alone does not prove a usable log chain: a database switched to FULL with no
      // full backup is still pseudo-simple. Require an observed baseline.
      if (!await HasFullBaselineAsync(connection, cancellationToken))
      {
        return TenantStorageErrors.BackupBaselineMissing.Code;
      }
    }

    if (request.Operation.OperationCode == "Differential" &&
      !await HasFullBaselineAsync(connection, cancellationToken))
    {
      // The base is validated from msdb rather than from Platform run history, because a full taken OUTSIDE
      // the platform still resets the differential base and platform history cannot see it.
      return TenantStorageErrors.BackupBaselineMissing.Code;
    }

    return null;
  }

  private static async Task<bool> HasFullBaselineAsync(
    SqlConnection connection,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT TOP (1) 1 FROM msdb.dbo.backupset WHERE database_name = DB_NAME() AND type = 'D'";
    var found = await command.ExecuteScalarAsync(cancellationToken);
    return found is not null and not DBNull;
  }

  private static async Task<int> ReadEngineEditionAsync(
    SqlConnection connection,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT CAST(SERVERPROPERTY('EngineEdition') AS int)";
    var value = await command.ExecuteScalarAsync(cancellationToken);
    return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
  }

  // Detects a server-side backup already running against THIS database (ADR-022 §14).
  //
  // Reads sys.dm_exec_requests, which on SQL Server 2022 requires VIEW SERVER PERFORMANCE STATE — the
  // granular successor to VIEW SERVER STATE, and emphatically not sysadmin. The permission is granted by
  // deployment; nothing here assumes or requests it.
  //
  // A permission failure is treated as "cannot determine", not "nothing running": claiming the database is
  // free because we are not allowed to look would be the worst possible reading.
  private static async Task<bool> IsBackupInFlightAsync(
    SqlConnection connection,
    CancellationToken cancellationToken)
  {
    try
    {
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT TOP (1) 1 FROM sys.dm_exec_requests " +
        "WHERE database_id = DB_ID() AND command LIKE 'BACKUP%' AND session_id <> @@SPID";
      var found = await command.ExecuteScalarAsync(cancellationToken);
      return found is not null and not DBNull;
    }
    catch (SqlException)
    {
      // Insufficient visibility. Fail CLOSED: report an operation as possibly in flight rather than
      // proceeding on an assumption we could not verify.
      return true;
    }
  }

  private async Task ExecuteBackupAsync(
    SqlConnection connection,
    TenantDatabaseBackupRequest request,
    string databaseName,
    string fullPath,
    bool compress,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();

    // The database name is an IDENTIFIER and is bracket-quoted; the PATH is a PARAMETER. SQL Server accepts
    // an expression for the backup device, so the one value that could carry a path never becomes SQL text.
    command.CommandText = SqlServerBackupCommandText.Build(request.Operation, databaseName, compress);
    command.Parameters.Add(SqlServerBackupCommandText.PathParameterName, SqlDbType.NVarChar, 260).Value = fullPath;

    // A backup is long-running by nature, so the default 30-second command timeout would abort every real
    // backup. An explicit bounded timeout is used rather than 0, which would remove the only client-side
    // bound on a hung operation.
    command.CommandTimeout = (int)operationalOptions.BackupCommandTimeout.TotalSeconds;

    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  // Deterministic correlation: this run's UNIQUE artifact name, plus the database and operation type it
  // should have produced. Never "the most recent backup".
  private static async Task<BackupEvidence?> ReadEvidenceAsync(
    SqlConnection connection,
    string databaseName,
    string fileName,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT TOP (1) bs.backup_set_uuid, bs.first_lsn, bs.last_lsn, bs.database_backup_lsn, " +
      "bs.backup_size, bs.backup_finish_date, bs.type, bs.is_copy_only, bs.has_backup_checksums, bs.backup_set_id " +
      "FROM msdb.dbo.backupset AS bs " +
      "INNER JOIN msdb.dbo.backupmediafamily AS bmf ON bmf.media_set_id = bs.media_set_id " +
      "WHERE bs.database_name = @database AND bmf.physical_device_name LIKE @artifact " +
      "ORDER BY bs.backup_set_id DESC";
    command.Parameters.Add("@database", SqlDbType.NVarChar, 128).Value = databaseName;
    // Matches the tail of the resolved path. The file name is generated by this provider and contains no
    // wildcard characters, so the pattern cannot widen beyond this run's own artifact.
    command.Parameters.Add("@artifact", SqlDbType.NVarChar, 300).Value = "%" + fileName;

    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
      return null;
    }

    return new BackupEvidence(
      reader.IsDBNull(0) ? null : reader.GetGuid(0),
      reader.IsDBNull(1) ? null : reader.GetDecimal(1),
      reader.IsDBNull(2) ? null : reader.GetDecimal(2),
      reader.IsDBNull(3) ? null : reader.GetDecimal(3),
      reader.IsDBNull(4) ? null : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
      reader.IsDBNull(5) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)),
      reader.IsDBNull(9) ? null : Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture));
  }

  private sealed record BackupEvidence(
    Guid? BackupSetGuid,
    decimal? FirstLsn,
    decimal? LastLsn,
    decimal? DatabaseBackupLsn,
    long? BackupSizeBytes,
    DateTimeOffset? FinishedUtc,
    long? BackupSetId)
  {
    // The provider's stable identity for this backup set. Prefers the backup-set GUID, which is what a
    // restore would be selected by, and falls back to the numeric identity where the GUID is unavailable.
    public string BackupSetIdentity =>
      BackupSetGuid?.ToString() ??
      BackupSetId?.ToString(CultureInfo.InvariantCulture) ??
      string.Empty;
  }

  private static TenantDatabaseBackupProviderResult Blocked(string reason, DateTimeOffset? startedUtc = null) =>
    new(TenantDatabaseBackupOutcome.BlockedByPrecondition,
      StartedUtc: startedUtc, CompletedUtc: DateTimeOffset.UtcNow, SafeErrorSummary: reason);

  private static TenantDatabaseBackupProviderResult Failed(string reason, DateTimeOffset startedUtc) =>
    new(TenantDatabaseBackupOutcome.Failed,
      StartedUtc: startedUtc, CompletedUtc: DateTimeOffset.UtcNow, SafeErrorSummary: reason);

  // A bounded, operator-facing summary. Deliberately the exception TYPE and its message only — never a
  // connection string, a resolved destination, credentials, or a stack that might carry either.
  private static string Summarise(Exception exception)
  {
    var summary = $"{exception.GetType().Name}: {exception.Message}";
    return summary.Length <= SafeErrorSummaryMaximumLength
      ? summary
      : summary[..SafeErrorSummaryMaximumLength];
  }
}

// Operational tuning for backup execution. Separate from policy: these are deployment concerns, not
// durability decisions.
public sealed class TenantDatabaseBackupOperationalOptions
{
  // A backup is long-running by nature. Bounded rather than infinite, so a hung operation still ends.
  public TimeSpan BackupCommandTimeout { get; init; } = TimeSpan.FromHours(4);

  public TimeSpan OwnershipTimeout { get; init; } = TimeSpan.FromSeconds(30);

  // Whether to check for a server-side backup already running before issuing one. Required if the empirical
  // session-loss result shows ownership can be released while a backup is still winding down (ADR-022 §14
  // exit condition B); harmless and cheap otherwise, so it defaults ON.
  public bool InFlightDetectionEnabled { get; init; } = true;

  public static TenantDatabaseBackupOperationalOptions Default { get; } = new();
}
