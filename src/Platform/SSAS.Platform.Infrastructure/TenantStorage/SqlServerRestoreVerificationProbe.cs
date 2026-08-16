using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// POST-RESTORE PROBES against the isolated verification database (ADR-022 §17, TS-Backup Phase D7).
//
// This is what turns "SQL Server mounted the files" into "the platform could actually recover this tenant".
// §17 names the evidence: the restore completes, the database comes online, the tenant migration history is
// readable and at the expected position, and basic schema probes succeed. A restore that produces an online
// database with an unreadable or wrong-versioned tenant schema is not a recovery position, and reporting it
// as one would be the comfortable mistake this whole capability exists to avoid.
//
// READ-ONLY, and deliberately narrow. It asks whether the database is USABLE, never what it contains: no
// business-data assertions, no per-tenant row inspection, and nothing written. A shared physical database
// hosts many tenants and its verification stays physical-database scoped (§19).
internal sealed class SqlServerRestoreVerificationProbe(
  ITenantDatabaseVerificationConnectionFactory connectionFactory)
  : ITenantDatabaseRestoreVerificationProbe
{
  public async Task<TenantDatabaseRestoreProbeResult> ExecuteAsync(
    TenantDatabaseRestoreProbeRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!TenantDatabaseVerificationNaming.MatchesRun(
      request.VerificationDatabaseName, request.TenantDatabaseId, request.VerificationRunId))
    {
      return TenantDatabaseRestoreProbeResult.Unavailable(
        TenantStorageErrors.RestoreVerificationTargetNameNotSafe.Code);
    }

    var created = connectionFactory.CreateForVerificationDatabase(
      new TenantDatabaseVerificationTarget(request.RestoreServerKey, request.SourceServerKey),
      request.VerificationDatabaseName);
    if (created.IsFailure)
    {
      return TenantDatabaseRestoreProbeResult.Unavailable(created.Error.Code);
    }

    await using var connection = created.Value;

    try
    {
      if (connection.State != System.Data.ConnectionState.Open)
      {
        await connection.OpenAsync(cancellationToken);
      }
    }
    catch (SqlException exception)
    {
      // Could not reach the restored database at all. Infrastructure, not evidence about the backup.
      return TenantDatabaseRestoreProbeResult.Unavailable(Safe(exception));
    }

    TenantDatabaseRecoveryModel observedRecoveryModel;
    try
    {
      // ONLINE, RE-CONFIRMED. The provider already checked, but the probe is a separate moment and a
      // database can leave ONLINE between them — and everything below assumes it is queryable.
      var state = await ScalarAsync(
        connection, "SELECT state_desc FROM sys.databases WHERE database_id = DB_ID()", cancellationToken);
      if (!string.Equals(state, "ONLINE", StringComparison.OrdinalIgnoreCase))
      {
        return TenantDatabaseRestoreProbeResult.Failed(
          TenantStorageErrors.RestoreVerificationDatabaseNotOnline.Code);
      }

      var recoveryModel = await ScalarAsync(
        connection,
        "SELECT recovery_model_desc FROM sys.databases WHERE database_id = DB_ID()",
        cancellationToken);
      observedRecoveryModel = recoveryModel?.ToUpperInvariant() switch
      {
        "SIMPLE" => TenantDatabaseRecoveryModel.Simple,
        "FULL" => TenantDatabaseRecoveryModel.Full,
        "BULK_LOGGED" => TenantDatabaseRecoveryModel.BulkLogged,
        _ => 0
      };
      if (!Enum.IsDefined(observedRecoveryModel))
      {
        return TenantDatabaseRestoreProbeResult.Failed(
          TenantStorageErrors.RestoreVerificationSchemaPositionUnexpected.Code);
      }
    }
    catch (SqlException exception)
    {
      // The database's health has not yet been evaluated. A catalog transport failure here is not evidence
      // that the backup restored to an unusable schema.
      return TenantDatabaseRestoreProbeResult.Unavailable(Safe(exception));
    }

    try
    {
      // THE TENANT CONTEXT OPENS THE RESTORED DATABASE. Reusing the same builder the schema-health service
      // uses is the point: the probe exercises the application's real model against the restored copy rather
      // than a hand-written approximation of it.
      await using var context = TenantDbContextBuilder.ForSchemaProbeConnection(connection);

      // MIGRATION HISTORY, READ FROM THE RESTORED DATABASE ITSELF. The expected head is derived from the
      // deployed tenant migration catalog rather than hard-coded, so this stays correct as the product moves.
      var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
      var known = TenantDbContextBuilder.KnownMigrations.ToArray();

      if (applied.Length == 0)
      {
        // Restored, online, and carrying no tenant migration history: whatever this is, it is not a
        // recoverable tenant database.
        return TenantDatabaseRestoreProbeResult.Failed(
          TenantStorageErrors.RestoreVerificationMigrationHistoryUnreadable.Code);
      }

      // A migration the deployed application does not know means the restored lineage diverged. This is the
      // same rule ADR-018 applies to a live database, applied to a restored one.
      if (known.Length == 0 ||
        applied.Length != known.Length ||
        !applied.SequenceEqual(known, StringComparer.Ordinal))
      {
        return TenantDatabaseRestoreProbeResult.Failed(
          TenantStorageErrors.RestoreVerificationSchemaPositionUnexpected.Code);
      }

      // THE MODEL MUST BE ABLE TO ISSUE A QUERY. Reading applied migrations proves the history table is
      // readable; this proves the restored schema actually serves the application's model, which is the
      // difference between "the file mounted" and "the tenant could be served from this".
      // Compile and execute the REAL mapped entity shape while returning no business rows. The context uses
      // a reserved non-customer probe tenant, so the ordinary global filter remains active; the constant-
      // false predicate makes the query physical-schema-only. Selecting the entity references every mapped
      // column without inspecting arbitrary shared-database ERP rows.
      _ = await context.Companies.AsNoTracking()
        .Where(_ => false)
        .FirstOrDefaultAsync(cancellationToken);

      return TenantDatabaseRestoreProbeResult.Succeeded(observedRecoveryModel, applied[^1]);
    }
    catch (SqlException exception) when (IsTransport(exception))
    {
      return TenantDatabaseRestoreProbeResult.Unavailable(Safe(exception));
    }
    catch (SqlException exception)
    {
      // A SQL error while probing a database that is online is a statement about the RESTORED SCHEMA — a
      // missing table, an unusable model — not about the verification host.
      return TenantDatabaseRestoreProbeResult.Failed(Safe(exception));
    }
    catch (InvalidOperationException exception)
    {
      // EF raises this when the model cannot be applied to what the database actually contains.
      return TenantDatabaseRestoreProbeResult.Failed(
        string.Create(CultureInfo.InvariantCulture, $"SchemaProbeFailed:{exception.GetType().Name}"));
    }
  }

  private static async Task<string?> ScalarAsync(
    SqlConnection connection,
    string sql,
    CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    var value = await command.ExecuteScalarAsync(cancellationToken);
    return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
  }

  // An error NUMBER and nothing else. Server messages can echo paths and object names, and a durable summary
  // must carry neither (ADR-022 §11).
  private static string Safe(SqlException exception) =>
    string.Create(CultureInfo.InvariantCulture, $"SqlError:{exception.Number}");

  private static bool IsTransport(SqlException exception) =>
    exception.Number is -2 or 0 or 53 or 64 or 233 or 1_0053 or 1_0054 or 1_0060;
}
