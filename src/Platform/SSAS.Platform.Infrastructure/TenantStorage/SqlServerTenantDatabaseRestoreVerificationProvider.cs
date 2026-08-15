using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Actual isolated restore verification against SQL Server (ADR-022 §17, TS-Backup Phase D6).
//
// THE ORDER OF THE CHECKS BELOW IS THE SAFETY MODEL, and it is deliberately conservative: everything that
// can refuse does so BEFORE the first RESTORE statement, because a restore that should not have started
// cannot be un-started.
//
//   1. resolve the verification target        — fails closed, never falls back to the source server
//   2. re-establish the target's identity      — the admitted operation and the database it belongs to
//   3. reject a name that is not provably ours — reserved vocabulary AND not a registered tenant database
//   4. refuse an existing target               — never overwrite, never drop-and-retry
//   5. read the source file list               — the only trustworthy source of logical names
//   6. restore the selected sequence           — every file relocated, recovery only at the end
//   7. confirm ONLINE
//
// NO CLEANUP HERE. This provider creates a verification database and deliberately does not remove it: the
// destructive-permission model is not yet proven against a directly-connected least-privilege identity, and
// implementing `DROP DATABASE` ahead of that proof would be doing the two things in the wrong order.
// Reconciliation of the databases it leaves behind is the companion component in this slice.
internal sealed class SqlServerTenantDatabaseRestoreVerificationProvider(
  ITenantDatabaseVerificationConnectionFactory connectionFactory,
  ITenantDatabaseBackupDestinationResolver destinationResolver,
  ITenantDatabaseRegistryReadRepository registry,
  IOptions<TenantDatabaseRestoreVerificationOptions> optionsAccessor,
  IDateTimeProvider clock) : ITenantDatabaseRestoreVerificationProvider
{
  // A restore of a real tenant database is measured in minutes, not seconds.
  private const int RestoreCommandTimeoutSeconds = 3_600;

  public async Task<TenantDatabaseRestoreVerificationResult> ExecuteAsync(
    TenantDatabaseRestoreVerificationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var startedUtc = clock.UtcNow;
    var options = optionsAccessor.Value;

    if (string.IsNullOrWhiteSpace(options.RestoreDataRoot) ||
      string.IsNullOrWhiteSpace(options.RestoreLogRoot))
    {
      return Unavailable(TenantStorageErrors.RestoreVerificationNotConfigured.Code, startedUtc);
    }

    // THE NAME MUST BE PROVABLY OURS BEFORE ANYTHING ELSE HAPPENS. Re-derived and re-validated here rather
    // than trusted from the request: this is the last layer before a statement that creates a database, and
    // a name that is not in the reserved vocabulary — or that collides with a registered tenant database —
    // must never reach a RESTORE (ADR-022 §17).
    var registered = await registry.ListPhysicalDatabasesAsync(0, int.MaxValue, cancellationToken);
    var safeName = TenantDatabaseVerificationTargetGuard.CanRestoreInto(
      request.VerificationDatabaseName,
      request.TenantDatabaseId,
      request.VerificationRunId,
      registered.Select(database => database.DatabaseName));

    if (!safeName)
    {
      return Blocked(TenantStorageErrors.RestoreVerificationTargetNameNotSafe.Code, startedUtc);
    }

    // Fails closed on an absent, unresolvable or non-isolated target. There is no path from here to the
    // source database's own server (compliance rule 44).
    var connection = connectionFactory.Create(
      new TenantDatabaseVerificationTarget(request.RestoreServerKey, request.SourceServerKey));
    if (connection.IsFailure)
    {
      return Unavailable(connection.Error.Code, startedUtc);
    }

    await using var sqlConnection = connection.Value;

    try
    {
      await sqlConnection.OpenAsync(cancellationToken);
    }
    catch (SqlException exception)
    {
      // The verification host is unreachable. NOT evidence about the backup.
      return Unavailable(Safe(exception), startedUtc);
    }

    try
    {
      // AN EXISTING TARGET FAILS SAFE. No overwrite, no `WITH REPLACE`, no drop-and-retry — the existing
      // database is left for reconciliation to correlate under the full conjunction.
      if (await DatabaseExistsAsync(sqlConnection, request.VerificationDatabaseName, cancellationToken))
      {
        return Blocked(TenantStorageErrors.RestoreVerificationTargetAlreadyExists.Code, startedUtc);
      }

      var devices = ResolveDevices(request.Chain);
      if (devices.IsFailure)
      {
        return Blocked(devices.Error.Code, startedUtc);
      }

      // The logical file list comes from the SOURCE FULL, and it is the only trustworthy source for the
      // MOVE clauses — a backup's layout is a property of the backup, never something to assume.
      var fileList = await ReadFileListAsync(sqlConnection, devices.Value[0].Device, cancellationToken);
      if (fileList.Count == 0)
      {
        return Blocked(TenantStorageErrors.RestoreVerificationArtifactUnavailable.Code, startedUtc);
      }

      var placements = TenantDatabaseVerificationFileLayout.Plan(
        fileList,
        request.VerificationDatabaseName,
        options.RestoreDataRoot!,
        options.RestoreLogRoot!);

      var restored = await RestoreSequenceAsync(
        sqlConnection, request, devices.Value, placements, cancellationToken);
      if (restored is not null)
      {
        return Failed(restored, startedUtc, request.VerificationDatabaseName);
      }

      // ONLINE is the minimum evidence that the sequence produced a database rather than a set of files.
      var state = await ReadDatabaseStateAsync(
        sqlConnection, request.VerificationDatabaseName, cancellationToken);
      if (!string.Equals(state, "ONLINE", StringComparison.OrdinalIgnoreCase))
      {
        return new TenantDatabaseRestoreVerificationResult(
          TenantDatabaseRestoreVerificationOutcome.NotOnline,
          request.VerificationDatabaseName,
          request.Chain.Steps.Count,
          startedUtc,
          clock.UtcNow,
          TenantStorageErrors.RestoreVerificationDatabaseNotOnline.Code);
      }

      // DELIBERATELY NOT "RestoreVerified". The migration-history and schema probes ADR-022 §17 also
      // requires belong to D7; claiming full verification here would report a probe that never ran.
      return new TenantDatabaseRestoreVerificationResult(
        TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline,
        request.VerificationDatabaseName,
        request.Chain.Steps.Count,
        startedUtc,
        clock.UtcNow);
    }
    catch (SqlException exception)
    {
      return Failed(Safe(exception), startedUtc, request.VerificationDatabaseName);
    }
  }

  // Rebuilds each artifact's physical location from the TRUSTED destination configuration plus the safe
  // artifact reference the backup provider recorded. No path is ever persisted or accepted from a caller
  // (ADR-022 §11, compliance rule 23).
  private Result<IReadOnlyList<TenantDatabaseRestoreDevice>> ResolveDevices(
    TenantDatabaseRestoreChain chain)
  {
    var devices = new List<TenantDatabaseRestoreDevice>(chain.Steps.Count);

    foreach (var step in chain.Steps)
    {
      if (string.IsNullOrWhiteSpace(step.Artifact.ArtifactReference))
      {
        return Result.Failure<IReadOnlyList<TenantDatabaseRestoreDevice>>(
          TenantStorageErrors.RestoreVerificationArtifactUnavailable);
      }

      var destination = destinationResolver.Resolve(step.Artifact.DestinationKey);
      if (destination.IsFailure)
      {
        return Result.Failure<IReadOnlyList<TenantDatabaseRestoreDevice>>(destination.Error);
      }

      // The artifact reference is a FILE NAME, not a path. Combining it with a trusted directory is what
      // keeps the destination decision in configuration where it belongs.
      var fileName = Path.GetFileName(step.Artifact.ArtifactReference);
      if (string.IsNullOrWhiteSpace(fileName) ||
        !string.Equals(fileName, step.Artifact.ArtifactReference.Trim(), StringComparison.Ordinal))
      {
        // A reference carrying directory structure is not the vocabulary the backup provider emits, and is
        // refused rather than normalised.
        return Result.Failure<IReadOnlyList<TenantDatabaseRestoreDevice>>(
          TenantStorageErrors.RestoreVerificationArtifactUnavailable);
      }

      devices.Add(new TenantDatabaseRestoreDevice(
        step.Kind, Path.Combine(destination.Value.DirectoryPath, fileName)));
    }

    return Result.Success<IReadOnlyList<TenantDatabaseRestoreDevice>>(devices);
  }

  private static async Task<IReadOnlyList<TenantDatabaseBackupFileEntry>> ReadFileListAsync(
    SqlConnection connection,
    string device,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = SqlServerRestoreCommandText.FileListOnly();
    command.Parameters.Add(SqlServerRestoreCommandText.DeviceParameterName, System.Data.SqlDbType.NVarChar, 260)
      .Value = device;

    var entries = new List<TenantDatabaseBackupFileEntry>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
      entries.Add(new TenantDatabaseBackupFileEntry(
        reader.GetString(reader.GetOrdinal("LogicalName")),
        reader.GetString(reader.GetOrdinal("Type"))));
    }

    return entries;
  }

  // Issues the sequence. Returns null on success, or a safe reason.
  //
  // RECOVERY ONLY AT THE END: every step but the last runs NORECOVERY, because recovering early makes the
  // remaining segments unrestorable (ADR-022 §17, Level C).
  private static async Task<string?> RestoreSequenceAsync(
    SqlConnection connection,
    TenantDatabaseRestoreVerificationRequest request,
    IReadOnlyList<TenantDatabaseRestoreDevice> devices,
    IReadOnlyList<TenantDatabaseVerificationFilePlacement> placements,
    CancellationToken cancellationToken)
  {
    for (var index = 0; index < devices.Count; index++)
    {
      var device = devices[index];
      var isFinal = index == devices.Count - 1;
      var step = device.Kind switch
      {
        TenantDatabaseRestoreStepKind.Differential => TenantDatabaseRestoreStep.Differential,
        TenantDatabaseRestoreStepKind.Log => TenantDatabaseRestoreStep.Log,
        _ => TenantDatabaseRestoreStep.Full
      };

      await using var command = connection.CreateCommand();
      command.CommandText = SqlServerRestoreCommandText.Restore(
        request.VerificationDatabaseName, placements, step, isFinal);
      command.CommandTimeout = RestoreCommandTimeoutSeconds;
      command.Parameters.Add(SqlServerRestoreCommandText.DeviceParameterName, System.Data.SqlDbType.NVarChar, 260)
        .Value = device.Device;

      // MOVE targets travel as parameters, only on the step that establishes the layout.
      if (step == TenantDatabaseRestoreStep.Full)
      {
        foreach (var placement in placements)
        {
          command.Parameters.Add(
            SqlServerRestoreCommandText.ParameterFor(placement), System.Data.SqlDbType.NVarChar, 260)
            .Value = placement.PhysicalPath;
        }
      }

      try
      {
        await command.ExecuteNonQueryAsync(cancellationToken);
      }
      catch (SqlException exception)
      {
        return Safe(exception);
      }
    }

    return null;
  }

  private static async Task<bool> DatabaseExistsAsync(
    SqlConnection connection,
    string databaseName,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_ID(@name)";
    command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;

    var value = await command.ExecuteScalarAsync(cancellationToken);
    return value is not null and not DBNull;
  }

  private static async Task<string?> ReadDatabaseStateAsync(
    SqlConnection connection,
    string databaseName,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @name";
    command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;

    var value = await command.ExecuteScalarAsync(cancellationToken);
    return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
  }

  // Operator-facing and bounded. A SQL error number and nothing else: server messages can echo paths, and a
  // path is exactly what must not reach a durable summary (ADR-022 §11).
  private static string Safe(SqlException exception) =>
    string.Create(CultureInfo.InvariantCulture, $"SqlError:{exception.Number}");

  private TenantDatabaseRestoreVerificationResult Blocked(string reason, DateTimeOffset startedUtc) =>
    new(TenantDatabaseRestoreVerificationOutcome.BlockedByPrecondition,
      null, 0, startedUtc, clock.UtcNow, reason);

  private TenantDatabaseRestoreVerificationResult Unavailable(string reason, DateTimeOffset startedUtc) =>
    new(TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable,
      null, 0, startedUtc, clock.UtcNow, reason);

  private TenantDatabaseRestoreVerificationResult Failed(
    string reason,
    DateTimeOffset startedUtc,
    string databaseName) =>
    new(TenantDatabaseRestoreVerificationOutcome.RestoreFailed,
      databaseName, 0, startedUtc, clock.UtcNow, reason);
}

// One resolved artifact location, paired with the chain role it plays.
internal sealed record TenantDatabaseRestoreDevice(TenantDatabaseRestoreStepKind Kind, string Device);
