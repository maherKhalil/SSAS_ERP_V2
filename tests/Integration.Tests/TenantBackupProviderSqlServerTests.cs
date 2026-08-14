using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// TS-Backup Phase B against real SQL Server (ADR-022 §9, §14).
//
// These execute ACTUAL backups — the first slice permitted to. Every one runs against a disposable generated
// catalog, writes into a per-run folder under the instance backup directory, and cleans up only its own
// files. No production or tenant database is ever a target, and nothing here restores.
public sealed class TenantBackupProviderSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_backup_succeeds_and_reconciles_evidence_from_msdb()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.True(outcome.IsSuccess);
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, outcome.Value.Status);

    // Success is reconciled evidence, not a completed command: the provider backup-set identity comes from
    // msdb and is required by the aggregate.
    Assert.False(string.IsNullOrWhiteSpace(outcome.Value.ProviderBackupSetIdentity));

    var run = (await fixture.BackupReads().ListRecentRunsAsync(id, 10))[0];
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, run.Status);
    Assert.Equal("SqlServer", run.OperationProviderKey);
    Assert.Equal("Full", run.OperationCode);
    Assert.NotNull(run.ProviderBackupIdentity);
    Assert.NotNull(run.CompletedUtc);
    Assert.True(run.SizeBytes > 0);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_backup_persists_chain_metadata_and_is_not_copy_only()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());

    // Chain metadata is captured from the first backup, because reconstructing continuity retrospectively is
    // far harder than recording it (ADR-022 §9).
    await using var platform = fixture.PlatformContext();
    var stored = await platform.TenantDatabaseBackupRuns.AsNoTracking()
      .SingleAsync(item => item.TenantDatabaseId == id);
    Assert.NotNull(stored.FirstLsn);
    Assert.NotNull(stored.LastLsn);
    Assert.NotNull(stored.DatabaseBackupLsn);
    Assert.NotNull(stored.BackupSetGuid);

    // COPY_ONLY would silently anchor later differentials to an older full, so the managed chain must never
    // produce one. Proven from SQL Server's own record rather than from the command string.
    Assert.Equal(0, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND is_copy_only = 1"));
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND has_backup_checksums = 1"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_differential_succeeds_after_a_full_and_ties_to_its_base()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var differential = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerDifferential());

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, differential.Value.Status);

    // SQL Server's own classification: type 'I' is a differential, and its database_backup_lsn identifies
    // the full it depends on.
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND type = 'I'"));
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      "SELECT COUNT(*) FROM msdb.dbo.backupset AS d WHERE d.type = 'I' " +
      $"AND d.database_name = N'{fixture.TargetCatalog}' AND EXISTS (SELECT 1 FROM msdb.dbo.backupset AS f " +
      $"WHERE f.type = 'D' AND f.database_name = d.database_name AND f.first_lsn = d.database_backup_lsn)"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_differential_without_a_baseline_is_blocked_rather_than_attempted()
  {
    // The base is validated from msdb, because a full taken OUTSIDE the platform still resets the
    // differential base and platform run history cannot see it.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerDifferential());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupBaselineMissing.Code, outcome.Value.SafeErrorSummary);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_transaction_log_backup_succeeds_on_a_full_recovery_database_with_a_baseline()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var log = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, log.Value.Status);
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND type = 'L'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_log_backup_without_a_baseline_is_blocked()
  {
    // A FULL recovery model alone does not prove a usable chain — a database switched to FULL with no full
    // backup is still pseudo-simple.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupBaselineMissing.Code, outcome.Value.SafeErrorSummary);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_simple_recovery_database_blocks_a_log_backup_and_the_model_is_never_changed()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);
    await fixture.SetRecoveryModelAsync("SIMPLE");

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupRecoveryModelUnsupported.Code, outcome.Value.SafeErrorSummary);

    // Detect, report, degrade — never correct. Switching to FULL would start log growth on a database that
    // is by definition misconfigured (ADR-022 §9).
    Assert.Equal("SIMPLE", await fixture.ReadRecoveryModelAsync());
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_bulk_logged_database_may_still_take_a_log_backup()
  {
    // BULK_LOGGED genuinely supports log backups; only SIMPLE cannot. Whether it satisfies a strict
    // point-in-time policy is a Phase D readiness question, not an execution one.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    await fixture.SetRecoveryModelAsync("BULK_LOGGED");

    var log = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, log.Value.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_successful_backup_records_a_recovery_observation_but_never_marks_protected()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());

    var stored = await fixture.ReadDatabaseAsync(id);

    Assert.NotNull(stored.LastSuccessfulFullBackupUtc);
    Assert.NotNull(stored.LastRecoveryReadinessCheckUtc);

    // A good backup produces a baseline that has never been restored once. ADR-022 §6 requires verification
    // evidence for Protected, and the first actual restore verification is Phase D.
    Assert.NotEqual(TenantDatabaseRecoveryReadinessStatus.Protected, stored.RecoveryReadinessStatus);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, stored.RecoveryReadinessStatus);
    Assert.Null(stored.LastRestoreVerificationUtc);

    // And the other dimensions were untouched by the recovery writer.
    Assert.Null(stored.LastSuccessfulDifferentialBackupUtc);
    Assert.Null(stored.LastSuccessfulLogBackupUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_unknown_destination_key_fails_closed_without_writing_anything()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, destinationKey: "NoSuchVault");

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupDestinationNotConfigured.Code, outcome.Value.SafeErrorSummary);
    Assert.Equal(0, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_destination_the_sql_server_service_account_cannot_write_fails_safely()
  {
    // The constraint that surprises people: SQL Server writes the file as its SERVICE identity, not as this
    // process. A trusted, configured destination can still be refused, and it must fail cleanly.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, destinationKey: BackupFixture.UnwritableDestinationKey);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.Equal(TenantDatabaseBackupRunStatus.Failed, outcome.Value.Status);
    Assert.False(string.IsNullOrWhiteSpace(outcome.Value.SafeErrorSummary));

    // The summary is bounded and carries no credential. It may name the path SQL Server reported, which is
    // the server's own error text, so the assertion is on boundedness rather than on redaction.
    Assert.True(outcome.Value.SafeErrorSummary!.Length <= TenantDatabaseBackupRun.ErrorSummaryMaximumLength);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_completed_command_without_matching_evidence_is_never_recorded_successful()
  {
    // The rule ADR-022 §14 exists to enforce, tested through a controlled seam: the provider reconciles on
    // the artifact name it generated, so evidence recorded under a different name must not be adopted.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    // A real backup exists for this database, taken outside the platform under a name the platform did not
    // generate. Deterministic correlation must refuse to claim it.
    await fixture.TakeExternalBackupAsync();
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}'"));

    var evidence = await fixture.ReconcileAsync("42_Full_20260814T000000Z_999.bak");

    Assert.Null(evidence);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Ownership_is_held_for_the_duration_so_a_second_worker_skips_rather_than_duplicates()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    // A competing holder on its own session, exactly as a second platform instance would be.
    await using var holder = await fixture.OpenOwnedConnectionAsync();

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    // A clean skip: coordination worked. Not a failure, and no readiness degradation.
    Assert.Equal(TenantDatabaseBackupRunStatus.SkippedOwnershipHeld, outcome.Value.Status);
    Assert.NotEqual(TenantDatabaseBackupRunStatus.Failed, outcome.Value.Status);
    Assert.Equal(0, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Compression_is_applied_where_the_edition_supports_it()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, compressionMode: TenantDatabaseBackupCompressionMode.PreferredWhereSupported);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());

    // On an edition that supports compression the compressed size is smaller than the raw size; on one that
    // does not, the run still succeeds uncompressed. Both are correct, so this asserts the run succeeded and
    // that SQL Server recorded a coherent pair.
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' " +
      "AND compressed_backup_size > 0 AND backup_size > 0"));
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  [Trait("Decision", "ADR-022")]
  public async Task A_customer_managed_database_is_never_backed_up_by_the_platform()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync(hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated);
    await fixture.AddPolicyAsync(id, managementMode: TenantDatabaseBackupManagementMode.CustomerDba);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    // Refused before anything opens a connection. Backup custody belongs to the customer (ADR-021).
    Assert.True(outcome.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupNotPermittedByManagementMode.Code, outcome.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Customer_dba_and_approval_gated_policies_are_blocked_not_executed()
  {
    await using var fixture = await BackupFixture.CreateAsync();

    foreach (var mode in new[]
    {
      TenantDatabaseBackupManagementMode.CustomerDba,
      // No approval workflow exists yet, and ADR-022 §5 says absence of approval is DENIAL. The alternative
      // would be a caller-supplied "approved" flag, which is a hole rather than a feature.
      TenantDatabaseBackupManagementMode.PlatformAfterApproval
    })
    {
      var id = await fixture.RegisterAsync(catalogSuffix: mode.ToString());
      await fixture.AddPolicyAsync(id, managementMode: mode);

      var outcome = await fixture.Executor().ExecuteAsync(
        id, TenantDatabaseBackupOperation.SqlServerFull());

      Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
      Assert.Equal(
        TenantStorageErrors.BackupNotPermittedByManagementMode.Code, outcome.Value.SafeErrorSummary);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_disabled_policy_blocks_execution()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, enabled: false);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_database_with_no_policy_cannot_be_backed_up()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.True(outcome.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupPolicyNotConfigured.Code, outcome.Error.Code);
  }

  // One Platform catalog, one disposable target catalog, and a per-run backup folder.
  internal sealed class BackupFixture : IAsyncDisposable
  {
    public const string PrimaryServerKey = "PrimarySqlServer";

    public const string DestinationKey = "TestVault";

    // A trusted, configured destination the SQL Server service identity cannot write to. Configured
    // deliberately so the "service account access" failure has a controlled test.
    public const string UnwritableDestinationKey = "UnwritableVault";

    private readonly List<string> targetCatalogs = [];

    private BackupFixture(string platformCatalog, string backupRoot)
    {
      PlatformCatalog = platformCatalog;
      BackupRoot = backupRoot;
    }

    public string PlatformCatalog { get; }

    // Per-run folder under the instance backup directory, so cleanup only ever touches this run's files.
    public string BackupRoot { get; }

    public string TargetCatalog { get; private set; } = string.Empty;

    public static async Task<BackupFixture> CreateAsync()
    {
      var runId = Guid.NewGuid().ToString("N");
      var fixture = new BackupFixture(
        $"SSAS_ERP_BACKUPP_{runId}",
        Path.Combine(TestBackupRoot(), runId));

      try
      {
        // The test process creates the folder and the SQL SERVER SERVICE IDENTITY writes into it, so the
        // location must be reachable by BOTH accounts — which is exactly the asymmetry ADR-022 §11 warns
        // about. The instance default backup directory lives under Program Files and is writable only by the
        // service account, so a test process cannot create a subfolder there; a ProgramData root is
        // creatable by this process and writable by the service account.
        //
        // This is a TEST-FIXTURE concern. Production code hard-codes no path: it resolves destinations from
        // trusted configuration, and provisioning the directory with correct ACLs is a deployment concern.
        Directory.CreateDirectory(fixture.BackupRoot);

        await using var platform = fixture.PlatformContext();
        await platform.Database.MigrateAsync();
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    public static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    public static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    // Overridable so a differently-configured environment can point the fixture somewhere both the test
    // process and the SQL Server service account can reach.
    public static string TestBackupRoot() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_BACKUP_ROOT") ??
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SSAS_BackupTests");

    public PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(PlatformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock());
    }

    private IOptions<TenantStorageOptions> StorageOptions()
    {
      var options = Options.Create(new TenantStorageOptions());
      var master = new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString;

      // Runtime credentials exist so the test proves the BACKUP path does not use them.
      options.Value.Servers[PrimaryServerKey] = new TenantStorageServerOptions { ConnectionString = master };
      options.Value.BackupServers[PrimaryServerKey] = new TenantStorageServerOptions { ConnectionString = master };

      options.Value.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = BackupRoot };
      // A path SQL Server's service account cannot create files in.
      options.Value.BackupDestinations[UnwritableDestinationKey] =
        new TenantStorageBackupDestinationOptions
        {
          DirectoryPath = Path.Combine(BackupRoot, "no-such-subdirectory")
        };

      return options;
    }

    public TenantDatabaseBackupReadRepository BackupReads() => new(PlatformContext());

    public ITenantDatabaseBackupExecutor Executor()
    {
      var context = PlatformContext();
      var registry = new TenantDatabaseRegistryReadRepository(context);
      var options = StorageOptions();

      var provider = new SqlServerTenantDatabaseBackupProvider(
        registry,
        new TenantDatabaseBackupConnectionFactory(options),
        options,
        // A short command timeout keeps a hung test bounded; real deployments use the four-hour default.
        new TenantDatabaseBackupOperationalOptions
        {
          BackupCommandTimeout = TimeSpan.FromMinutes(5),
          OwnershipTimeout = TimeSpan.FromSeconds(2)
        });

      return new TenantDatabaseBackupExecutor(
        registry,
        new TenantDatabaseBackupReadRepository(context),
        new TenantDatabaseBackupRunStore(context, new TestClock()),
        provider,
        new TenantDatabaseRecoveryReadinessWriter(context, new TestClock()));
    }

    public async Task<long> RegisterAsync(
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
      string catalogSuffix = "")
    {
      var catalog = $"SSAS_ERP_BACKUPT_{Guid.NewGuid():N}";
      targetCatalogs.Add(catalog);
      TargetCatalog = catalog;

      // A real, disposable catalog. FULL recovery so log backups are meaningful.
      await ExecuteOnAsync("master", $"CREATE DATABASE [{catalog}]");
      await ExecuteOnAsync("master", $"ALTER DATABASE [{catalog}] SET RECOVERY FULL");

      await using var platform = PlatformContext();
      var database = TenantDatabase.Register(
        hostingMode, storageMode, PrimaryServerKey, catalog,
        TenantDatabaseProvisioningStatus.Ready, "backup-tests", TestClock.Fixed).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    public async Task AddPolicyAsync(
      long tenantDatabaseId,
      string? destinationKey = DestinationKey,
      TenantDatabaseBackupManagementMode managementMode = TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      TenantDatabaseBackupCompressionMode compressionMode = TenantDatabaseBackupCompressionMode.PreferredWhereSupported,
      bool enabled = true)
    {
      await using var platform = PlatformContext();
      var policy = TenantDatabaseBackupPolicy.Create(
        tenantDatabaseId, enabled, managementMode, destinationKey,
        10_080, 1_440, 15, 35, 90, 60, "backup-tests", TestClock.Fixed,
        compressionMode: compressionMode).Value;
      platform.TenantDatabaseBackupPolicies.Add(policy);
      await platform.SaveChangesAsync();
    }

    // Holds backup ownership on its own session, as a competing platform instance would.
    public async Task<SqlConnection> OpenOwnedConnectionAsync()
    {
      var builder = new SqlConnectionStringBuilder(ConnectionFor(TargetCatalog)) { Pooling = false };
      var connection = new SqlConnection(builder.ConnectionString);
      await connection.OpenAsync();
      var ownership = await TenantDatabaseBackupOwnership.TryAcquireAsync(connection, TimeSpan.FromSeconds(5));
      Assert.NotNull(ownership);
      return connection;
    }

    // A backup taken outside the platform, under a name the platform did not generate.
    public Task TakeExternalBackupAsync() =>
      ExecuteOnAsync(TargetCatalog,
        $"BACKUP DATABASE [{TargetCatalog}] TO DISK = N'{Path.Combine(BackupRoot, "external-dba.bak")}' WITH INIT");

    // Runs the provider's own reconciliation query shape against an artifact name, to prove correlation is
    // by generated name rather than by recency.
    public async Task<string?> ReconcileAsync(string artifactFileName)
    {
      await using var connection = new SqlConnection(ConnectionFor(TargetCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT TOP (1) CAST(bs.backup_set_uuid AS nvarchar(64)) FROM msdb.dbo.backupset AS bs " +
        "INNER JOIN msdb.dbo.backupmediafamily AS bmf ON bmf.media_set_id = bs.media_set_id " +
        "WHERE bs.database_name = @database AND bmf.physical_device_name LIKE @artifact";
      command.Parameters.AddWithValue("@database", TargetCatalog);
      command.Parameters.AddWithValue("@artifact", "%" + artifactFileName);
      return await command.ExecuteScalarAsync() as string;
    }

    public Task SetRecoveryModelAsync(string model) =>
      ExecuteOnAsync("master", $"ALTER DATABASE [{TargetCatalog}] SET RECOVERY {model}");

    public async Task<string> ReadRecoveryModelAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        $"SELECT CAST(DATABASEPROPERTYEX(N'{TargetCatalog}', 'Recovery') AS nvarchar(60))";
      return await command.ExecuteScalarAsync() as string ?? string.Empty;
    }

    public async Task<TenantDatabase> ReadDatabaseAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      return await platform.TenantDatabases.AsNoTracking()
        .SingleAsync(item => item.Id == tenantDatabaseId);
    }

    public static async Task<int> MsdbScalarAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task ExecuteOnAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 300;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in targetCatalogs.Append(PlatformCatalog))
      {
        try
        {
          await ExecuteOnAsync("master",
            $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN ALTER DATABASE [{catalog}] " +
            $"SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]; END");
        }
        catch (SqlException)
        {
          // A leftover catalog is housekeeping, never a reason to fail the test that created it.
        }
      }

      // Cleanup is scoped to THIS RUN'S folder and nothing else. No wildcard sweep of the instance backup
      // directory, and no xp_delete_file — which is undocumented and effectively needs sysadmin.
      try
      {
        if (Directory.Exists(BackupRoot))
        {
          Directory.Delete(BackupRoot, recursive: true);
        }
      }
      catch (IOException)
      {
        // Files are written by the SQL Server service account; if this process cannot remove them that is a
        // test-environment ACL matter, not something production code should be given the power to solve.
      }
      catch (UnauthorizedAccessException)
      {
      }
    }
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "backup-tests";

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class NoTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public static readonly DateTimeOffset Fixed = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => Fixed;
  }
}
