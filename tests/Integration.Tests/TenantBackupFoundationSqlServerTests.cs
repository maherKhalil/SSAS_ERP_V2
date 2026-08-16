using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// TS-Backup Phase A against real SQL (ADR-022).
//
// The properties proven here cannot be established in memory: CHECK constraints, unique indexes, migration
// backfill and RowVersion conflict behaviour are all database artifacts. NOTHING HERE EXECUTES A BACKUP —
// no BACKUP DATABASE, no BACKUP LOG, no RESTORE. Phase A models backup state; Phase B performs it.
public sealed class TenantBackupFoundationSqlServerTests
{
  // SQL Server's two uniqueness-violation numbers: duplicate key in a unique index, and duplicate key in a
  // unique constraint.
  private static readonly int[] UniquenessViolationNumbers = [2601, 2627];

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_platform_migration_applies_and_creates_the_backup_foundation()
  {
    await using var fixture = await BackupFixture.CreateAsync();

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.tables WHERE name = N'TenantDatabaseBackupPolicies' AND SCHEMA_NAME(schema_id) = N'platform'"));
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.tables WHERE name = N'TenantDatabaseBackupRuns' AND SCHEMA_NAME(schema_id) = N'platform'"));
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns WHERE name = N'RecoveryReadinessStatus' " +
      "AND object_id = OBJECT_ID(N'platform.TenantDatabases')"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_existing_database_backfills_to_unknown_never_to_protected()
  {
    // The migration's backfill value, proven against a row that exists before anything evaluates it.
    // 'Protected' here would be a fabricated durability claim about every database in the estate.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");

    var stored = await fixture.ReadAsync(id);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unknown, stored.RecoveryReadinessStatus);
    Assert.Null(stored.LastRecoveryReadinessCheckUtc);
    Assert.Null(stored.LastSuccessfulFullBackupUtc);
    Assert.Null(stored.LastRestoreVerificationUtc);

    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabases] WHERE [RecoveryReadinessStatus] <> N'Unknown'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_backup_policy_persists_once_per_physical_database()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");

    await fixture.AddPolicyAsync(id);

    var policy = await fixture.BackupReads().FindPolicyAsync(id);
    Assert.NotNull(policy);
    Assert.True(policy!.Enabled);
    Assert.Equal(TenantDatabaseBackupManagementMode.AutomaticByPlatform, policy.ManagementMode);
    Assert.Equal("PrimaryBackupVault", policy.DestinationKey);
    Assert.Equal(10_080, policy.FullBackupIntervalMinutes);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_second_policy_for_the_same_physical_database_is_rejected_by_the_database()
  {
    // A shared database hosting many tenants is ONE backup target. Two policy rows could disagree with each
    // other about the same physical database, so the unique index refuses the second.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");
    await fixture.AddPolicyAsync(id);

    var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.AddPolicyAsync(id));

    Assert.IsType<SqlException>(duplicate.InnerException);
    Assert.Contains(((SqlException)duplicate.InnerException!).Number, UniquenessViolationNumbers);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_database_refuses_a_status_outside_the_closed_set()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");

    var readiness = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      "UPDATE [platform].[TenantDatabases] SET [RecoveryReadinessStatus] = N'ProbablyFine', " +
      $"[LastRecoveryReadinessCheckUtc] = SYSDATETIMEOFFSET() WHERE [TenantDatabaseId] = {id}"));
    Assert.Contains("CK_TenantDatabases_RecoveryReadinessStatus", readiness.Message, StringComparison.Ordinal);

    // PROTECTED REQUIRES EVIDENCE. Even a direct SQL write cannot mark a database protected that has never
    // had a successful full backup recorded (ADR-022 compliance rule 11).
    var protectedWithoutEvidence = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      "UPDATE [platform].[TenantDatabases] SET [RecoveryReadinessStatus] = N'Protected', " +
      $"[LastRecoveryReadinessCheckUtc] = SYSDATETIMEOFFSET() WHERE [TenantDatabaseId] = {id}"));
    Assert.Contains(
      "CK_TenantDatabases_ProtectedRequiresFullBackup",
      protectedWithoutEvidence.Message,
      StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_database_refuses_a_successful_run_without_provider_evidence()
  {
    // The run-level expression of the same rule: a completed command is not evidence a backup exists.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");

    var violation = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      "INSERT INTO [platform].[TenantDatabaseBackupRuns] " +
      "([TenantDatabaseId], [OperationProviderKey], [OperationCode], [Status], [StartedUtc], " +
      "[VerificationState], [CreatedUtc], [ModifiedUtc]) VALUES " +
      $"({id}, N'SqlServer', N'Full', N'Succeeded', SYSDATETIMEOFFSET(), N'NotVerified', " +
      "SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())"));

    Assert.Contains(
      "CK_TenantDatabaseBackupRuns_SucceededHasEvidence", violation.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_backup_run_persists_with_provider_scoped_operation_and_chain_metadata()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");

    // Created by the test, not by any background component: nothing in Phase A produces runs on its own.
    var setGuid = Guid.NewGuid();
    await fixture.AddRunAsync(id, TenantDatabaseBackupOperation.SqlServerFull(), run =>
      run.Succeed("backup-set-1", "vault/full/1", 4_096, 100m, 220m, 100m, null, setGuid, "backup-tests",
        BackupFixture.Now.AddMinutes(3)));

    var latest = await fixture.BackupReads()
      .FindLatestSuccessfulRunAsync(id, TenantDatabaseBackupOperation.SqlServerProviderKey, "Full");

    Assert.NotNull(latest);
    Assert.Equal("SqlServer", latest!.OperationProviderKey);
    Assert.Equal("Full", latest.OperationCode);
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, latest.Status);
    Assert.Equal("backup-set-1", latest.ProviderBackupIdentity);
    Assert.Equal(4_096, latest.SizeBytes);

    var runs = await fixture.BackupReads().ListRecentRunsAsync(id, 10);
    Assert.Single(runs);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_skipped_run_is_recorded_as_a_controlled_outcome_not_a_failure()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");

    await fixture.AddRunAsync(id, TenantDatabaseBackupOperation.SqlServerFull(), run =>
      run.Skip(
        TenantDatabaseBackupRunStatus.SkippedInFlightOperation,
        "a server-side backup was already running",
        "backup-tests",
        BackupFixture.Now.AddMinutes(1)));

    var runs = await fixture.BackupReads().ListRecentRunsAsync(id, 10);

    Assert.Equal(TenantDatabaseBackupRunStatus.SkippedInFlightOperation, runs[0].Status);
    Assert.NotEqual(TenantDatabaseBackupRunStatus.Failed, runs[0].Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_recovery_writer_persists_its_dimension_and_touches_no_other()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");
    await fixture.SetConnectivityAsync(id, TenantDatabaseConnectivityStatus.Healthy);
    await fixture.SetSchemaAsync(id, TenantDatabaseSchemaCompatibilityStatus.UpToDate, "20260814_A", "20260814_A");

    var before = await fixture.ReadAsync(id);

    await using (var context = fixture.PlatformContext())
    {
      await new TenantDatabaseRecoveryReadinessWriter(context, new TestClock())
        .RecordRecoveryReadinessAsync(
          id,
          TenantDatabaseRecoveryReadinessStatus.Protected,
          "recovery-tests",
          lastSuccessfulFullBackupUtc: BackupFixture.Now.AddDays(-1),
          lastRestoreVerificationUtc: BackupFixture.Now.AddDays(-7));
    }

    var after = await fixture.ReadAsync(id);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, after.RecoveryReadinessStatus);
    Assert.NotNull(after.LastRecoveryReadinessCheckUtc);
    Assert.NotNull(after.LastSuccessfulFullBackupUtc);
    Assert.NotNull(after.LastRestoreVerificationUtc);

    // Every other dimension is byte-for-byte what it was.
    Assert.Equal(before.ConnectivityStatus, after.ConnectivityStatus);
    Assert.Equal(before.LastConnectivityCheckUtc, after.LastConnectivityCheckUtc);
    Assert.Equal(before.SchemaCompatibilityStatus, after.SchemaCompatibilityStatus);
    Assert.Equal(before.LastSchemaCheckUtc, after.LastSchemaCheckUtc);
    Assert.Equal(before.AppliedMigration, after.AppliedMigration);
    Assert.Equal(before.TargetMigration, after.TargetMigration);
    Assert.Equal(before.MigrationExecutionStatus, after.MigrationExecutionStatus);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  [Trait("Decision", "ADR-022")]
  public async Task A_losing_writer_deterministically_retries_re_reads_and_reapplies_only_its_own_dimension()
  {
    // L7 — the deterministic RowVersion conflict proof, and the reason it had to land in this slice: with a
    // third writer on one row, cross-dimension clobbering becomes progressively harder to detect.
    //
    // The existing concurrency test proves a SAFE OUTCOME under a real race, but a race is not a guarantee
    // that the conflict branch ran. Here the conflict is FORCED: an interceptor on the recovery writer's own
    // context updates the row from a separate connection between its read and its save, so the save cannot
    // do anything but throw DbUpdateConcurrencyException. The retry path under test is the production one —
    // TenantDatabaseDimensionWriter, shared by every dimension writer.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");
    await fixture.SetSchemaAsync(id, TenantDatabaseSchemaCompatibilityStatus.UpToDate, "20260814_A", "20260814_A");

    // Writer A's observation, applied in the gap between writer B's read and writer B's save.
    var interceptor = new ConflictInterceptor(
      fixture.PlatformCatalog,
      $"UPDATE [platform].[TenantDatabases] SET [ConnectivityStatus] = N'Unreachable', " +
      $"[LastConnectivityCheckUtc] = SYSDATETIMEOFFSET() WHERE [TenantDatabaseId] = {id}");

    await using (var context = fixture.PlatformContext(interceptor))
    {
      await new TenantDatabaseRecoveryReadinessWriter(context, new TestClock())
        .RecordRecoveryReadinessAsync(
          id,
          TenantDatabaseRecoveryReadinessStatus.Degraded,
          "recovery-tests",
          lastSuccessfulFullBackupUtc: BackupFixture.Now.AddDays(-3));
    }

    // The conflict was genuinely forced, and the writer genuinely retried rather than giving up.
    Assert.Equal(1, interceptor.ConflictsInjected);
    Assert.True(interceptor.SaveAttempts >= 2, $"expected a retry; saw {interceptor.SaveAttempts} save attempt(s)");

    var after = await fixture.ReadAsync(id);

    // Writer B re-read fresh and reapplied ONLY its own dimension...
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, after.RecoveryReadinessStatus);
    Assert.NotNull(after.LastRecoveryReadinessCheckUtc);
    Assert.Equal(BackupFixture.Now.AddDays(-3), after.LastSuccessfulFullBackupUtc);

    // ...so writer A's dimension survived, which a stale whole-aggregate replay would have erased.
    Assert.Equal(TenantDatabaseConnectivityStatus.Unreachable, after.ConnectivityStatus);
    Assert.NotNull(after.LastConnectivityCheckUtc);

    // And the dimension neither writer touched is untouched.
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.UpToDate, after.SchemaCompatibilityStatus);
    Assert.Equal("20260814_A", after.AppliedMigration);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Recovery_readiness_as_the_third_writer_preserves_both_existing_dimensions()
  {
    // The same property with all three dimensions live, which is the configuration TS-Backup actually
    // creates: connectivity healthy, schema up to date, recovery moving off Unknown for the first time.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync("SSAS_Backup_Target_A");
    await fixture.SetConnectivityAsync(id, TenantDatabaseConnectivityStatus.Healthy);
    await fixture.SetSchemaAsync(id, TenantDatabaseSchemaCompatibilityStatus.UpToDate, "20260814_A", "20260814_A");

    var initial = await fixture.ReadAsync(id);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unknown, initial.RecoveryReadinessStatus);

    // A schema writer wins the race that the recovery writer then has to survive.
    var interceptor = new ConflictInterceptor(
      fixture.PlatformCatalog,
      "UPDATE [platform].[TenantDatabases] SET [SchemaCompatibilityStatus] = N'PendingMigrations', " +
      $"[LastSchemaCheckUtc] = SYSDATETIMEOFFSET() WHERE [TenantDatabaseId] = {id}");

    await using (var context = fixture.PlatformContext(interceptor))
    {
      await new TenantDatabaseRecoveryReadinessWriter(context, new TestClock())
        .RecordRecoveryReadinessAsync(
          id,
          TenantDatabaseRecoveryReadinessStatus.VerificationOverdue,
          "recovery-tests",
          lastSuccessfulFullBackupUtc: BackupFixture.Now.AddDays(-2));
    }

    var after = await fixture.ReadAsync(id);

    Assert.Equal(1, interceptor.ConflictsInjected);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, after.RecoveryReadinessStatus);
    // The schema writer's observation survived...
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.PendingMigrations, after.SchemaCompatibilityStatus);
    // ...and so did the connectivity observation neither of them touched.
    Assert.Equal(TenantDatabaseConnectivityStatus.Healthy, after.ConnectivityStatus);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Recovery_state_lives_on_the_physical_database_and_not_on_assignments()
  {
    await using var fixture = await BackupFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'platform.TenantDatabaseAssignments') " +
      "AND (name LIKE N'%Backup%' OR name LIKE N'%Recovery%')"));

    // And no tenant ERP database gained backup tables: Phase A adds no tenant migration at all.
    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.tables WHERE name LIKE N'%BackupPolic%' AND SCHEMA_NAME(schema_id) = N'tenant'"));
  }

  // Forces a RowVersion conflict deterministically by updating the row from a SEPARATE connection during
  // the writer's first SaveChanges — the moment between its read and its write. One injection only, so the
  // retry then succeeds and the test proves recovery rather than exhaustion of the retry bound.
  private sealed class ConflictInterceptor(string catalog, string conflictingSql) : SaveChangesInterceptor
  {
    public int SaveAttempts { get; private set; }

    public int ConflictsInjected { get; private set; }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
      DbContextEventData eventData,
      InterceptionResult<int> result,
      CancellationToken cancellationToken = default)
    {
      SaveAttempts++;
      if (ConflictsInjected == 0)
      {
        ConflictsInjected++;
        await BackupFixture.ExecuteOnAsync(catalog, conflictingSql);
      }

      return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
  }

  private sealed class BackupFixture : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private const string PrimaryServerKey = "PrimarySqlServer";

    private BackupFixture(string platformCatalog) => PlatformCatalog = platformCatalog;

    public string PlatformCatalog { get; }

    public static async Task<BackupFixture> CreateAsync()
    {
      var fixture = new BackupFixture($"SSAS_ERP_BACKUP_P_{Guid.NewGuid():N}");
      try
      {
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

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    public PlatformDbContext PlatformContext(SaveChangesInterceptor? interceptor = null)
    {
      var builder = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(
          ConnectionFor(PlatformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"));
      if (interceptor is not null)
      {
        builder.AddInterceptors(interceptor);
      }

      return new PlatformDbContext(builder.Options, new TestUser(), new NoTenant(), new TestClock());
    }

    public TenantDatabaseBackupReadRepository BackupReads() => new(PlatformContext());

    public async Task<long> RegisterAsync(
      string databaseName,
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared)
    {
      // The physical database row only — no catalog is created, because Phase A never connects to a tenant
      // database, let alone backs one up.
      await using var platform = PlatformContext();
      var database = TenantDatabase.Register(
        hostingMode, storageMode, PrimaryServerKey, databaseName,
        TenantDatabaseProvisioningStatus.Ready, "backup-tests", Now).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    public async Task AddPolicyAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      var policy = TenantDatabaseBackupPolicy.Create(
        tenantDatabaseId, true, TenantDatabaseBackupManagementMode.AutomaticByPlatform,
        "PrimaryBackupVault", 10_080, 1_440, 15, 35, 90, 60, "backup-tests", Now).Value;
      platform.TenantDatabaseBackupPolicies.Add(policy);
      await platform.SaveChangesAsync();
    }

    public async Task AddRunAsync(
      long tenantDatabaseId,
      TenantDatabaseBackupOperation operation,
      Action<TenantDatabaseBackupRun> complete)
    {
      await using var platform = PlatformContext();
      var run = TenantDatabaseBackupRun.Start(
        tenantDatabaseId, operation, "PrimaryBackupVault", "backup-tests", Now).Value;
      complete(run);
      platform.TenantDatabaseBackupRuns.Add(run);
      await platform.SaveChangesAsync();
    }

    public async Task SetConnectivityAsync(long tenantDatabaseId, TenantDatabaseConnectivityStatus status)
    {
      await using var platform = PlatformContext();
      await new TenantDatabaseHealthWriter(platform, new TestClock())
        .RecordConnectivityAsync(tenantDatabaseId, status, "backup-tests");
    }

    public async Task SetSchemaAsync(
      long tenantDatabaseId,
      TenantDatabaseSchemaCompatibilityStatus status,
      string? applied = null,
      string? target = null)
    {
      await using var platform = PlatformContext();
      await new TenantDatabaseHealthWriter(platform, new TestClock())
        .RecordSchemaAsync(tenantDatabaseId, status, applied, target, "backup-tests");
    }

    public async Task<TenantDatabase> ReadAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      return await platform.TenantDatabases.AsNoTracking()
        .SingleAsync(item => item.Id == tenantDatabaseId);
    }

    public Task ExecuteAsync(string sql) => ExecuteOnAsync(PlatformCatalog, sql);

    public static async Task ExecuteOnAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async Task<int> ScalarAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(PlatformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
      try
      {
        await using var connection = new SqlConnection(
          new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
          $"IF DB_ID(N'{PlatformCatalog}') IS NOT NULL BEGIN " +
          $"ALTER DATABASE [{PlatformCatalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
          $"DROP DATABASE [{PlatformCatalog}]; END";
        await command.ExecuteNonQueryAsync();
      }
      catch (SqlException)
      {
        // A leftover catalog is a housekeeping problem, never a reason to fail the test that created it.
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
    public DateTimeOffset UtcNow => BackupFixtureNow;

    private static DateTimeOffset BackupFixtureNow => new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
  }
}
