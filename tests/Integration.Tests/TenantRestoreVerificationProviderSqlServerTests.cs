using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ACTUAL ISOLATED RESTORE against real SQL Server (ADR-022 §17, TS-Backup Phase D6).
//
// These are the tests the whole capability exists to make possible: a chain the platform selected is
// restored into a disposable database and the result is observed, rather than a command being asserted
// against a string. Everything here uses the PRODUCTION provider — chain selection, artifact resolution,
// file layout and command construction all run exactly as they would in a deployment.
//
// NOTHING IS DROPPED BY PRODUCTION CODE. The provider deliberately implements no cleanup: the destructive
// permission model is unproven on a Windows-auth-only instance, and shipping a `DROP DATABASE` path ahead of
// that proof would be the wrong order. The fixture removes its own artifacts by exact name.
//
// SHARES THE BACKUP SERIAL COLLECTION rather than defining its own. A separate collection would still run in
// PARALLEL with the backup suites — xUnit serialises within a collection, not between them — and these
// restores write full-size database files while the backup suites write full-size backups. That combination
// was observed making timing-sensitive tests elsewhere in the suite fail for reasons unconnected to what
// they assert. Parallelism stays enabled everywhere else.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantRestoreVerificationProviderSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task D7_probe_rechecks_online_migration_history_and_real_tenant_model()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.PrepareTenantSchemaAsync();
    await fixture.TakeFullAsync();
    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline,
      (await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full)).Outcome);

    var probe = await fixture.ProbeAsync();

    Assert.Equal(TenantDatabaseRestoreProbeOutcome.Succeeded, probe.Outcome);
    Assert.Equal(TenantDatabaseRecoveryModel.Full, probe.ObservedRecoveryModel);
    Assert.Equal(TenantDbContextBuilder.KnownMigrations[^1], probe.AppliedMigration);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task D7_probe_refuses_a_restored_database_without_tenant_migration_history()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full);

    var probe = await fixture.ProbeAsync();

    Assert.Equal(TenantDatabaseRestoreProbeOutcome.Failed, probe.Outcome);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationMigrationHistoryUnreadable.Code,
      probe.SafeErrorSummary);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task D7_real_model_probe_fails_when_migration_history_claims_a_schema_that_is_not_usable()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.PrepareTenantSchemaAsync();
    await fixture.BreakApplicationSchemaAsync();
    await fixture.TakeFullAsync();
    await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full);

    var probe = await fixture.ProbeAsync();

    Assert.Equal(TenantDatabaseRestoreProbeOutcome.Failed, probe.Outcome);
    Assert.StartsWith("SqlError:", probe.SafeErrorSummary, StringComparison.Ordinal);
  }

  // 1. Level A — full only.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_backup_restores_into_an_isolated_online_database()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);
    Assert.Equal(1, result.RestoredStepCount);
    Assert.Equal("ONLINE", await RestoreFixture.StateOfAsync(RestoreFixture.VerificationDatabaseName));

    // The source database is untouched — a restore verification proves a copy, never disturbs the original.
    Assert.Equal("ONLINE", await RestoreFixture.StateOfAsync(fixture.SourceDatabase));
  }

  // 2. Level B — full + applicable differential.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_and_differential_chain_restores_and_comes_online()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeDifferentialAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);
    Assert.Equal(2, result.RestoredStepCount);
    Assert.Equal("ONLINE", await RestoreFixture.StateOfAsync(RestoreFixture.VerificationDatabaseName));
  }

  // 3. Level C with NO differential — full + logs. A differential is not a precondition for log verification.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_and_log_chain_restores_without_any_differential()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeLogAsync();
    await fixture.TakeLogAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);
    Assert.True(result.RestoredStepCount >= 2);
    Assert.Equal("ONLINE", await RestoreFixture.StateOfAsync(RestoreFixture.VerificationDatabaseName));
  }

  // 4. Level C with a differential — full + differential + the log tail beyond it.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_differential_and_log_chain_restores_and_comes_online()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeLogAsync();
    await fixture.TakeDifferentialAsync();
    await fixture.WriteSomeDataAsync();
    await fixture.TakeLogAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);
    Assert.Equal("ONLINE", await RestoreFixture.StateOfAsync(RestoreFixture.VerificationDatabaseName));
  }

  // 5 + 6 + 12. Multiple data AND log files, every one relocated. The source's own physical paths must never
  // be reused — a restore that wrote over them would destroy the database it exists to protect.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Every_data_and_log_file_is_relocated_away_from_the_source_paths()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full);
    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);

    // The source has two data files and one log; the restored copy must have the same count, at different
    // paths, none of which collide with the source's.
    var sourcePaths = await RestoreFixture.PhysicalFilesOfAsync(fixture.SourceDatabase);
    var restoredPaths = await RestoreFixture.PhysicalFilesOfAsync(RestoreFixture.VerificationDatabaseName);

    Assert.Equal(3, sourcePaths.Count);
    Assert.Equal(3, restoredPaths.Count);
    Assert.Empty(restoredPaths.Intersect(sourcePaths, StringComparer.OrdinalIgnoreCase));
    Assert.Equal(restoredPaths.Count, restoredPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
  }

  // 7. A missing artifact fails safely and creates nothing.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_missing_artifact_fails_without_creating_a_database()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    fixture.DeleteNewestArtifactFile();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.ArtifactUnavailable, result.Outcome);
    Assert.StartsWith("SqlError:", result.SafeErrorSummary, StringComparison.Ordinal);
    Assert.Null(await RestoreFixture.StateOfAsync(RestoreFixture.VerificationDatabaseName));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Sql_restore_rejection_of_the_required_full_is_an_artifact_restore_failure()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeDifferentialAsync();

    // Present the newest differential artifact as the durable Full. FILELISTONLY can read it, but SQL
    // Server rejects it when the provider issues the required baseline RESTORE DATABASE operation.
    var result = await fixture.VerifyAsync(
      TenantDatabaseRestoreDepth.Full, useNewestArtifactAsFull: true);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoreFailed, result.Outcome);
    Assert.Equal(0, result.RestoredStepCount);
    Assert.StartsWith("SqlError:", result.SafeErrorSummary, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_missing_required_deeper_artifact_is_reported_as_artifact_unavailable()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeDifferentialAsync();
    fixture.DeleteNewestArtifactFile();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.ArtifactUnavailable, result.Outcome);
    Assert.Equal(1, result.RestoredStepCount);
    Assert.Equal(TenantDatabaseRestoreDepth.Full, result.AchievedDepth);
  }

  // 8. AN EXISTING TARGET IS NEVER OVERWRITTEN. No REPLACE, no drop-and-retry.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_existing_target_database_is_never_overwritten()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    // Something already occupies the generated name — an orphan from a crashed run, for instance.
    await fixture.CreateDecoyAsync(RestoreFixture.VerificationDatabaseName);
    var decoyMarker = await RestoreFixture.MarkerOfAsync(RestoreFixture.VerificationDatabaseName);

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.BlockedByPrecondition, result.Outcome);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationTargetAlreadyExists.Code, result.SafeErrorSummary);

    // Untouched: same database, same contents.
    Assert.Equal(decoyMarker, await RestoreFixture.MarkerOfAsync(RestoreFixture.VerificationDatabaseName));
  }

  // 9. An external non-copy-only FULL resets the differential base, orphaning the platform's differential.
  // The platform's own artifacts can no longer form the requested sequence.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_external_full_that_orphans_the_platform_differential_is_a_chain_break()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeExternalFullAsync();
    await fixture.TakeDifferentialAsync();

    // The platform's differential now anchors to the EXTERNAL full, which the platform never recorded and
    // cannot locate, so the differential path cannot be exercised from platform-owned artifacts.
    //
    // THIS TEST PREVIOUSLY ASSERTED THE OPPOSITE OF ITS OWN NAME. It required success with a single full
    // step, which documented the silent downgrade as intended behaviour and let it pass a green suite. The
    // name was right and the assertions were wrong.
    var chain = fixture.SelectChain(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.True(chain.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreChainBroken.Code, chain.Error.Code);
  }

  // ...and the distinction that makes the rule above safe: a database that has simply never had a
  // differential taken is NOT broken. The full alone is genuinely its whole chain, and the result says so by
  // reporting Level A rather than by implying the request was met.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_database_with_no_differential_at_all_reports_level_a_rather_than_breaking()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    var chain = fixture.SelectChain(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.True(chain.IsSuccess);
    Assert.Single(chain.Value.Steps);
    Assert.Equal(TenantDatabaseRestoreDepth.Full, chain.Value.AchievedDepth);
  }

  // The provider carries the achieved depth through to its result, so D7 can refuse RestoreVerified at a
  // depth the sequence never exercised.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_provider_reports_the_depth_the_sequence_actually_restored()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeDifferentialAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);
    Assert.Equal(TenantDatabaseRestoreDepth.FullWithDifferential, result.AchievedDepth);
  }

  // 10. An external non-copy-only LOG takes a range the platform never recorded, leaving a gap its own
  // artifacts cannot span. Reported as a break rather than silently skipped.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_external_log_that_breaks_the_platform_sequence_is_reported_as_a_break()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeLogAsync();
    await fixture.WriteSomeDataAsync();
    await fixture.TakeExternalLogAsync();
    await fixture.WriteSomeDataAsync();
    await fixture.TakeLogAsync();

    var chain = fixture.SelectChain(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.True(chain.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreChainBroken.Code, chain.Error.Code);
  }

  // 11. An external COPY_ONLY full disturbs nothing: it resets no differential base and truncates no log,
  // so the platform's chain remains complete and must not degrade.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_external_copy_only_full_does_not_disturb_the_platform_chain()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();
    await fixture.TakeExternalCopyOnlyFullAsync();
    await fixture.TakeDifferentialAsync();

    var result = await fixture.VerifyAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline, result.Outcome);
    Assert.Equal(2, result.RestoredStepCount);
  }

  // 15. The provider refuses a name outside the reserved vocabulary, and refuses one that collides with a
  // registered tenant database — before any restore is issued.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_name_that_is_not_provably_ours_is_refused_before_any_restore()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    var result = await fixture.VerifyAsync(
      TenantDatabaseRestoreDepth.Full, overrideTargetName: fixture.SourceDatabase);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.BlockedByPrecondition, result.Outcome);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationTargetNameNotSafe.Code, result.SafeErrorSummary);

    // The source database is untouched.
    Assert.Equal("ONLINE", await RestoreFixture.StateOfAsync(fixture.SourceDatabase));
  }

  // Fails closed when the verification target cannot be resolved — never onto the source server.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_unresolvable_verification_target_fails_closed()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    var result = await fixture.VerifyAsync(
      TenantDatabaseRestoreDepth.Full, overrideRestoreServerKey: "no-such-verification-server");

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable, result.Outcome);
    Assert.Null(await RestoreFixture.StateOfAsync(RestoreFixture.VerificationDatabaseName));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_verification_server_transport_failure_is_infrastructure_unavailable()
  {
    await using var fixture = await RestoreFixture.CreateAsync();
    await fixture.TakeFullAsync();

    var unreachable = new SqlConnectionStringBuilder
    {
      DataSource = "127.0.0.1,1",
      InitialCatalog = "master",
      IntegratedSecurity = true,
      Encrypt = false,
      ConnectTimeout = 1,
      Pooling = false
    }.ConnectionString;

    var result = await fixture.VerifyAsync(
      TenantDatabaseRestoreDepth.Full, overrideVerificationConnection: unreachable);

    Assert.Equal(TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable, result.Outcome);
    Assert.StartsWith("SqlError:", result.SafeErrorSummary, StringComparison.Ordinal);
  }

  private sealed class RestoreFixture : IAsyncDisposable
  {
    private const long TenantDatabaseId = 4_242;

    private const long VerificationRunId = 77;

    private const string DestinationKey = "verification-source";

    private const string SourceServerKey = "PrimarySqlServer";

    private const string RestoreServerKey = "VerificationSqlServer";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private readonly List<TenantDatabaseBackupChainCandidate> platformRuns = [];

    private readonly List<string> createdDatabases = [];

    private long nextRunId = 1;

    private string workingDirectory = string.Empty;

    private RestoreFixture()
    {
    }

    public string SourceDatabase { get; private set; } = string.Empty;

    public static string VerificationDatabaseName =>
      TenantDatabaseVerificationNaming.ForRun(TenantDatabaseId, VerificationRunId);

    public static async Task<RestoreFixture> CreateAsync()
    {
      var fixture = new RestoreFixture();
      try
      {
        await fixture.InitialiseAsync();
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private async Task InitialiseAsync()
    {
      // Both accounts must reach this: the test process creates it, the SQL Server service writes into it.
      workingDirectory = Path.Combine(TestRoot(), token);
      Directory.CreateDirectory(workingDirectory);

      // TWO data files and one log, deliberately: a single-MDF assumption in the layout would pass a
      // one-file fixture and fail on the first real tenant database.
      SourceDatabase = $"SSAS_RestoreSrc_{token}";
      await ExecuteAsync("master",
        $"CREATE DATABASE [{SourceDatabase}] ON PRIMARY " +
        $"(NAME = N'{SourceDatabase}_d1', FILENAME = N'{Path.Combine(workingDirectory, "src1.mdf")}', SIZE = 8MB), " +
        $"(NAME = N'{SourceDatabase}_d2', FILENAME = N'{Path.Combine(workingDirectory, "src2.ndf")}', SIZE = 8MB) " +
        $"LOG ON (NAME = N'{SourceDatabase}_l1', FILENAME = N'{Path.Combine(workingDirectory, "src.ldf")}', SIZE = 8MB)");
      createdDatabases.Add(SourceDatabase);

      // FULL recovery so log backups are possible at all.
      await ExecuteAsync("master", $"ALTER DATABASE [{SourceDatabase}] SET RECOVERY FULL");
      await ExecuteAsync(SourceDatabase,
        "CREATE TABLE dbo.Marker (Id INT IDENTITY PRIMARY KEY, Value NVARCHAR(64) NOT NULL)");
      await WriteSomeDataAsync();
    }

    public Task WriteSomeDataAsync() =>
      ExecuteAsync(SourceDatabase, $"INSERT INTO dbo.Marker (Value) VALUES (N'{Guid.NewGuid():N}')");

    public async Task PrepareTenantSchemaAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor(SourceDatabase));
      await using var context = TenantDbContextBuilder.ForConnection(connection);
      await context.Database.MigrateAsync();
    }

    // ---- BREAKS THE SCHEMA WHILE LEAVING THE MIGRATION HISTORY CLAIMING IT IS FINE.
    //
    // Dependents first. Company acquired dependents when FP-006C3 added Employee with a restricted foreign
    // key to it, so dropping Companies alone now fails on the constraint rather than producing the broken
    // schema this test needs — the arrange step would fail before the assertion could run.
    //
    // Dropping the HR tables here is arrange, not scope creep: the test's premise is "the tables the
    // application needs are gone", and Employee is now one of them.
    public Task BreakApplicationSchemaAsync() =>
      ExecuteAsync(SourceDatabase, """
        DROP TABLE [tenant].[EmployeeBranchAssignments];
        DROP TABLE [tenant].[Employees];
        DROP TABLE [tenant].[Companies];
        """);

    // ---- Platform-managed backups. Each records a chain candidate exactly as the Phase B provider would:
    // the trusted destination key plus a safe artifact FILE NAME, never a resolved path.

    public Task TakeFullAsync() => TakePlatformAsync(TenantDatabaseRestoreStepKind.Full);

    public Task TakeDifferentialAsync() =>
      TakePlatformAsync(TenantDatabaseRestoreStepKind.Differential);

    public Task TakeLogAsync() => TakePlatformAsync(TenantDatabaseRestoreStepKind.Log);

    private async Task TakePlatformAsync(TenantDatabaseRestoreStepKind operation)
    {
      var runId = nextRunId++;
      var extension = operation == TenantDatabaseRestoreStepKind.Log ? "trn" : "bak";
      var fileName = $"{TenantDatabaseId}_{operation}_{token}_{runId}.{extension}";
      var device = Path.Combine(workingDirectory, fileName);

      await ExecuteAsync("master", BackupCommand(operation, device));
      platformRuns.Add(await ReadChainCandidateAsync(runId, operation, device, fileName));
    }

    // ---- External backups. Taken by "a DBA", never recorded as platform runs, so they can never become
    // chain candidates. They exist to disturb SQL Server's chain state, which is the whole point.

    public Task TakeExternalFullAsync() =>
      ExecuteAsync("master", BackupCommand(
        TenantDatabaseRestoreStepKind.Full,
        Path.Combine(workingDirectory, $"external-full-{nextRunId++}.bak")));

    public Task TakeExternalLogAsync() =>
      ExecuteAsync("master", BackupCommand(
        TenantDatabaseRestoreStepKind.Log,
        Path.Combine(workingDirectory, $"external-log-{nextRunId++}.trn")));

    public Task TakeExternalCopyOnlyFullAsync() =>
      ExecuteAsync("master",
        $"BACKUP DATABASE [{SourceDatabase}] TO DISK = N'{Path.Combine(workingDirectory, $"external-copyonly-{nextRunId++}.bak")}' " +
        "WITH COPY_ONLY, CHECKSUM, INIT, FORMAT");

    private string BackupCommand(TenantDatabaseRestoreStepKind operation, string device) =>
      operation switch
      {
        TenantDatabaseRestoreStepKind.Differential =>
          $"BACKUP DATABASE [{SourceDatabase}] TO DISK = N'{device}' WITH DIFFERENTIAL, CHECKSUM, INIT, FORMAT",
        TenantDatabaseRestoreStepKind.Log =>
          $"BACKUP LOG [{SourceDatabase}] TO DISK = N'{device}' WITH CHECKSUM, INIT, FORMAT",
        _ => $"BACKUP DATABASE [{SourceDatabase}] TO DISK = N'{device}' WITH CHECKSUM, INIT, FORMAT"
      };

    // Reads the chain metadata SQL Server recorded, which is what the Phase B provider persists on a run.
    private static async Task<TenantDatabaseBackupChainCandidate> ReadChainCandidateAsync(
      long runId,
      TenantDatabaseRestoreStepKind operation,
      string device,
      string fileName)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT TOP (1) bs.checkpoint_lsn, bs.database_backup_lsn, bs.first_lsn, bs.last_lsn " +
        "FROM msdb.dbo.backupset AS bs " +
        "INNER JOIN msdb.dbo.backupmediafamily AS bmf ON bmf.media_set_id = bs.media_set_id " +
        "WHERE bmf.physical_device_name = @device ORDER BY bs.backup_set_id DESC";
      command.Parameters.AddWithValue("@device", device);

      await using var reader = await command.ExecuteReaderAsync();
      Assert.True(await reader.ReadAsync());

      return new TenantDatabaseBackupChainCandidate(
        runId, operation, DestinationKey, fileName,
        reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3));
    }

    public Result<TenantDatabaseRestoreChain> SelectChain(TenantDatabaseRestoreDepth depth) =>
      TenantDatabaseBackupChainSelector.Select(platformRuns, depth);

    // Runs the PRODUCTION provider end to end.
    public async Task<TenantDatabaseRestoreVerificationResult> VerifyAsync(
      TenantDatabaseRestoreDepth depth,
      string? overrideTargetName = null,
      string? overrideRestoreServerKey = null,
      string? overrideVerificationConnection = null,
      bool useNewestArtifactAsFull = false)
    {
      var chain = SelectChain(depth);
      Assert.True(chain.IsSuccess);

      var selected = chain.Value;
      if (useNewestArtifactAsFull)
      {
        var artifact = platformRuns[^1];
        selected = new TenantDatabaseRestoreChain(
          [new TenantDatabaseRestoreChainStep(TenantDatabaseRestoreStepKind.Full, artifact)],
          artifact.LastLsn,
          artifact.BackupRunId,
          TenantDatabaseRestoreDepth.Full);
      }

      var storage = new TenantStorageOptions();
      storage.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = workingDirectory };

      // The "verification server" is this same instance, reached through the explicit non-production
      // exception. A dedicated instance is the production rule; a developer machine has one.
      storage.VerificationServers[RestoreServerKey] =
        new TenantStorageServerOptions
        {
          ConnectionString = overrideVerificationConnection ?? Configured()
        };

      var verificationOptions = new TenantDatabaseRestoreVerificationOptions
      {
        Enabled = true,
        RestoreServerKey = RestoreServerKey,
        RestoreDataRoot = workingDirectory,
        RestoreLogRoot = workingDirectory,
        AllowSameInstanceVerification = true
      };

      var provider = new SqlServerTenantDatabaseRestoreVerificationProvider(
        new TenantDatabaseVerificationConnectionFactory(
          Options.Create(storage), Options.Create(verificationOptions)),
        new TenantDatabaseBackupDestinationResolver(Options.Create(storage)),
        new StubRegistry(SourceDatabase),
        Options.Create(verificationOptions),
        new TestClock());

      var target = overrideTargetName ?? VerificationDatabaseName;
      createdDatabases.Add(target);

      return await provider.ExecuteAsync(new TenantDatabaseRestoreVerificationRequest(
        TenantDatabaseId,
        VerificationRunId,
        selected,
        overrideRestoreServerKey ?? RestoreServerKey,
        SourceServerKey,
        target));
    }

    public Task<TenantDatabaseRestoreProbeResult> ProbeAsync()
    {
      var (storage, verification) = VerificationConfiguration();
      var probe = new SqlServerRestoreVerificationProbe(
        new TenantDatabaseVerificationConnectionFactory(
          Options.Create(storage), Options.Create(verification)));

      return probe.ExecuteAsync(new TenantDatabaseRestoreProbeRequest(
        TenantDatabaseId,
        VerificationRunId,
        RestoreServerKey,
        SourceServerKey,
        VerificationDatabaseName));
    }

    private (TenantStorageOptions Storage, TenantDatabaseRestoreVerificationOptions Verification)
      VerificationConfiguration()
    {
      var storage = new TenantStorageOptions();
      storage.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = workingDirectory };
      storage.VerificationServers[RestoreServerKey] =
        new TenantStorageServerOptions { ConnectionString = Configured() };

      return (storage, new TenantDatabaseRestoreVerificationOptions
      {
        Enabled = true,
        RestoreServerKey = RestoreServerKey,
        RestoreDataRoot = workingDirectory,
        RestoreLogRoot = workingDirectory,
        AllowSameInstanceVerification = true
      });
    }

    public void DeleteNewestArtifactFile()
    {
      var newest = platformRuns[^1].ArtifactReference!;
      File.Delete(Path.Combine(workingDirectory, newest));
    }

    public async Task CreateDecoyAsync(string databaseName)
    {
      await ExecuteAsync("master", $"CREATE DATABASE [{databaseName}]");
      createdDatabases.Add(databaseName);
      await ExecuteAsync(databaseName,
        "CREATE TABLE dbo.Marker (Id INT IDENTITY PRIMARY KEY, Value NVARCHAR(64) NOT NULL)");
      await ExecuteAsync(databaseName, "INSERT INTO dbo.Marker (Value) VALUES (N'decoy')");
    }

    public static async Task<string?> MarkerOfAsync(string databaseName)
    {
      await using var connection = new SqlConnection(ConnectionFor(databaseName));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT TOP (1) Value FROM dbo.Marker ORDER BY Id DESC";
      return (string?)await command.ExecuteScalarAsync();
    }

    public static async Task<string?> StateOfAsync(string databaseName)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @name";
      command.Parameters.AddWithValue("@name", databaseName);
      return (string?)await command.ExecuteScalarAsync();
    }

    public static async Task<IReadOnlyList<string>> PhysicalFilesOfAsync(string databaseName)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT mf.physical_name FROM sys.master_files AS mf " +
        "INNER JOIN sys.databases AS d ON d.database_id = mf.database_id WHERE d.name = @name";
      command.Parameters.AddWithValue("@name", databaseName);

      var paths = new List<string>();
      await using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        paths.Add(reader.GetString(0));
      }

      return paths;
    }

    private static string TestRoot() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_BACKUP_ROOT") ??
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SSAS_BackupTests");

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }
        .ConnectionString;

    private static async Task ExecuteAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 600;
      await command.ExecuteNonQueryAsync();
    }

    // Teardown drops BY EXACT NAME only — never by pattern, which is the rule production cleanup will follow.
    public async ValueTask DisposeAsync()
    {
      foreach (var database in createdDatabases.Distinct(StringComparer.OrdinalIgnoreCase))
      {
        // DIRECT DROP FIRST. This fixture leaves verification databases in RESTORING, and a RESTORING
        // database cannot be put into SINGLE_USER — the dance fails with "ALTER DATABASE is not permitted
        // while a database is in the Restoring state" and the catalog leaks. That is not hypothetical: it
        // leaked SSAS_Verify_4242_77 on a real run. Same pattern as ProcessLoss.DropAsync.
        try
        {
          await ExecuteAsync("master", $"IF DB_ID(N'{database}') IS NOT NULL DROP DATABASE [{database}]");
          continue;
        }
        catch (SqlException)
        {
        }

        // Fallback for an ONLINE database still holding sessions, where the direct drop is refused.
        try
        {
          await ExecuteAsync("master",
            $"IF DB_ID(N'{database}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{database}]; END");
        }
        catch (SqlException error)
        {
          TestCatalogJanitor.RecordLeak(database, error);
          // Teardown is best-effort: a cleanup failure must not mask the assertion that ran before it.
        }
      }

      if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
      {
        try
        {
          Directory.Delete(workingDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
      }
    }

    // The registry the provider consults for authoritative-name collision. Returns the source database, so
    // the guard has something real to refuse.
    private sealed class StubRegistry(string sourceDatabase) : ITenantDatabaseRegistryReadRepository
    {
      public Task<IReadOnlyList<TenantDatabaseDescriptor>> ListPhysicalDatabasesAsync(
        long afterId,
        int take,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantDatabaseDescriptor>>(
        [
          new TenantDatabaseDescriptor(
            TenantDatabaseId, SourceServerKey, sourceDatabase,
            TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Dedicated,
            TenantDatabaseProvisioningStatus.Ready,
            TenantDatabaseMigrationManagementMode.AutomaticByPlatform,
            TenantDatabaseConnectivityStatus.Healthy,
            TenantDatabaseSchemaCompatibilityStatus.UpToDate,
            TenantDatabaseMigrationExecutionStatus.Succeeded,
            null)
        ]);

      public Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<TenantDatabaseAssignmentRecord?>(null);
    }

    private sealed class TestClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    }
  }
}
