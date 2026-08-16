using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// The complete D7 production path: production BACKUP provider -> reconciled Platform run evidence (including
// CheckpointLsn) -> durable admission -> production selector -> isolated RESTORE -> real probes -> exact
// source-run RestoreVerified evidence and readiness projection.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantRestoreVerificationExecutorSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Platform_backup_through_d7_executor_produces_authoritative_restore_verified_evidence()
  {
    await using var fixture = await FullPathFixture.CreateAsync();

    var backup = await fixture.TakePlatformFullAsync();
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, backup.Status);

    var baseline = await fixture.LatestFullAsync();
    Assert.NotNull(baseline);
    var candidates = await fixture.ChainCandidatesAsync();
    Assert.Equal(baseline!.TenantDatabaseBackupRunId, Assert.Single(candidates).BackupRunId);
    Assert.NotNull(candidates[0].CheckpointLsn);

    var verificationRunId = await fixture.AdmitAsync(baseline.TenantDatabaseBackupRunId);
    var result = await fixture.ExecuteVerificationAsync(verificationRunId);

    Assert.True(result.IsSuccess);
    Assert.True(result.Value.RestoreVerified);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, result.Value.Status);

    var persisted = await fixture.ReadPersistedOutcomeAsync(verificationRunId, baseline.TenantDatabaseBackupRunId);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, persisted.RunStatus);
    Assert.Equal(TenantDatabaseBackupVerificationState.RestoreVerified, persisted.BackupVerificationState);
    Assert.NotNull(persisted.LastRestoreVerificationUtc);
    Assert.Equal(persisted.RunCompletedUtc, persisted.LastRestoreVerificationUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Scheduler_discovers_a_due_platform_database_and_drives_the_complete_d7_sql_path()
  {
    await using var fixture = await FullPathFixture.CreateAsync();

    var backup = await fixture.TakePlatformFullAsync();
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, backup.Status);
    var baseline = await fixture.LatestFullAsync();
    Assert.NotNull(baseline);
    Assert.NotNull((await fixture.ChainCandidatesAsync()).Single().CheckpointLsn);

    var summary = await fixture.RunSchedulerAsync();

    Assert.Equal(1, summary.Due);
    Assert.Equal(1, summary.Dispatched);
    Assert.Equal(1, summary.Succeeded);
    var persisted = await fixture.ReadSchedulerPersistedOutcomeAsync(baseline!.TenantDatabaseBackupRunId);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, persisted.RunStatus);
    Assert.Equal(fixture.TenantDatabaseId, persisted.TenantDatabaseId);
    Assert.Equal(baseline.TenantDatabaseBackupRunId, persisted.SourceBackupRunId);
    Assert.Equal(TenantDatabaseRestoreDepth.Full, persisted.RequestedDepth);
    Assert.Equal("VerificationSqlServer", persisted.RestoreServerKey);
    Assert.Equal(TenantDatabaseVerificationNaming.ForRun(fixture.TenantDatabaseId, persisted.VerificationRunId),
      persisted.VerificationDatabaseName);
    Assert.Equal(TenantDatabaseBackupVerificationState.RestoreVerified, persisted.BackupVerificationState);
    Assert.Equal(persisted.RunCompletedUtc, persisted.LastRestoreVerificationUtc);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, persisted.RecoveryReadinessStatus);
  }

  private sealed class FullPathFixture : IAsyncDisposable
  {
    private const string SourceServerKey = "PrimarySqlServer";
    private const string RestoreServerKey = "VerificationSqlServer";
    private const string DestinationKey = "full-path";
    private const string Actor = "d7-full-path-test";

    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantDatabaseRestoreVerificationOptions verification = new();
    private string root = string.Empty;
    private string platformCatalog = string.Empty;
    private string sourceCatalog = string.Empty;
    private string? verificationCatalog;

    public long TenantDatabaseId { get; private set; }

    public static async Task<FullPathFixture> CreateAsync()
    {
      var fixture = new FullPathFixture();
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
      root = Path.Combine(TestRoot(), $"d7-{token}");
      Directory.CreateDirectory(root);
      platformCatalog = $"SSAS_D7_Platform_{token}";
      sourceCatalog = $"SSAS_D7_Source_{token}";

      await ExecuteSqlAsync("master", $"CREATE DATABASE [{sourceCatalog}]");
      await ExecuteSqlAsync("master", $"ALTER DATABASE [{sourceCatalog}] SET RECOVERY FULL");
      await using (var connection = new SqlConnection(ConnectionFor(sourceCatalog)))
      await using (var tenant = TenantDbContextBuilder.ForConnection(connection))
      {
        await tenant.Database.MigrateAsync();
      }

      await using (var platform = PlatformContext())
      {
        await platform.Database.MigrateAsync();
        var database = TenantDatabase.Register(
          TenantDatabaseHostingMode.PlatformManaged,
          TenantDatabaseStorageMode.Dedicated,
          SourceServerKey,
          sourceCatalog,
          TenantDatabaseProvisioningStatus.Ready,
          Actor,
          now).Value;
        platform.TenantDatabases.Add(database);
        await platform.SaveChangesAsync();
        TenantDatabaseId = database.Id;

        var policy = TenantDatabaseBackupPolicy.Create(
          TenantDatabaseId,
          enabled: true,
          TenantDatabaseBackupManagementMode.AutomaticByPlatform,
          DestinationKey,
          fullBackupIntervalMinutes: 1_440,
          differentialBackupIntervalMinutes: null,
          transactionLogBackupIntervalMinutes: null,
          retentionExpectationDays: 30,
          restoreVerificationIntervalDays: 30,
          maximumBackupAgeMinutes: 2_880,
          Actor,
          now).Value;
        platform.TenantDatabaseBackupPolicies.Add(policy);
        await platform.SaveChangesAsync();
      }

      storage.BackupServers[SourceServerKey] =
        new TenantStorageServerOptions { ConnectionString = Configured() };
      storage.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = root };
      storage.VerificationServers[RestoreServerKey] =
        new TenantStorageServerOptions { ConnectionString = Configured() };

      verification.Enabled = true;
      verification.RestoreServerKey = RestoreServerKey;
      verification.RestoreDataRoot = root;
      verification.RestoreLogRoot = root;
      verification.AllowSameInstanceVerification = true;
      verification.SchedulerBatchSize = 10;
      verification.MaxConcurrentVerifications = 1;
      verification.MaxConcurrentVerificationsPerServer = 1;
    }

    public async Task<TenantDatabaseBackupExecutionOutcome> TakePlatformFullAsync()
    {
      await using var platform = PlatformContext();
      var registry = new TenantDatabaseRegistryReadRepository(platform);
      var reads = new TenantDatabaseBackupReadRepository(platform);
      var clock = new TestClock(now);
      var executor = new TenantDatabaseBackupExecutor(
        registry,
        reads,
        new TenantDatabaseBackupRunStore(platform, clock),
        new SqlServerTenantDatabaseBackupProvider(
          registry,
          new TenantDatabaseBackupConnectionFactory(Options.Create(storage)),
          Options.Create(storage),
          TenantDatabaseBackupOperationalOptions.Default),
        new TenantDatabaseRecoveryReadinessWriter(platform, clock));

      var result = await executor.ExecuteAsync(
        TenantDatabaseId, TenantDatabaseBackupOperation.SqlServerFull());
      Assert.True(result.IsSuccess);
      return result.Value;
    }

    public async Task<TenantDatabaseBackupRunRecord?> LatestFullAsync()
    {
      await using var platform = PlatformContext();
      return await new TenantDatabaseBackupReadRepository(platform).FindLatestSuccessfulRunAsync(
        TenantDatabaseId, "SqlServer", "Full");
    }

    public async Task<IReadOnlyList<TenantDatabaseBackupChainCandidate>> ChainCandidatesAsync()
    {
      await using var platform = PlatformContext();
      return await new TenantDatabaseBackupReadRepository(platform).ListChainCandidatesAsync(TenantDatabaseId);
    }

    public async Task<long> AdmitAsync(long sourceBackupRunId)
    {
      await using var platform = PlatformContext();
      var admitted = await new TenantDatabaseRestoreVerificationRunStore(platform, new TestClock(now))
        .TryAdmitAsync(new TenantDatabaseRestoreVerificationAdmissionRequest(
          TenantDatabaseId,
          sourceBackupRunId,
          ExpectedPreviousSuccessfulVerificationRunId: null,
          TenantDatabaseRestoreDepth.Full,
          RestoreServerKey,
          Actor));
      Assert.True(admitted.IsSuccess);
      verificationCatalog = TenantDatabaseVerificationNaming.ForRun(TenantDatabaseId, admitted.Value);
      return admitted.Value;
    }

    public async Task<SSAS.BuildingBlocks.Domain.Result<TenantDatabaseRestoreVerificationExecutionOutcome>>
      ExecuteVerificationAsync(long verificationRunId)
    {
      await using var platform = PlatformContext();
      var registry = new TenantDatabaseRegistryReadRepository(platform);
      var reads = new TenantDatabaseBackupReadRepository(platform);
      var clock = new TestClock(now.AddMinutes(10));
      var connections = new TenantDatabaseVerificationConnectionFactory(
        Options.Create(storage), Options.Create(verification));

      var executor = new TenantDatabaseRestoreVerificationExecutor(
        registry,
        reads,
        new TenantDatabaseRestoreVerificationRunStore(platform, clock),
        new SqlServerTenantDatabaseRestoreVerificationProvider(
          connections,
          new TenantDatabaseBackupDestinationResolver(Options.Create(storage)),
          registry,
          Options.Create(verification),
          clock),
        new SqlServerRestoreVerificationProbe(connections),
        connections,
        new TenantDatabaseRecoveryReadinessWriter(platform, clock),
        Options.Create(verification),
        clock);

      return await executor.ExecuteAsync(
        TenantDatabaseId, verificationRunId, TenantDatabaseRestoreDepth.Full);
    }

    public async Task<TenantDatabaseRestoreVerificationSweepSummary> RunSchedulerAsync()
    {
      await using var platform = PlatformContext();
      var clock = new TestClock(now.AddMinutes(10));
      var registry = new TenantDatabaseRegistryReadRepository(platform);
      var reads = new TenantDatabaseBackupReadRepository(platform);
      var fleet = new TenantDatabaseRestoreVerificationFleetReadRepository(platform);
      var runStore = new TenantDatabaseRestoreVerificationRunStore(platform, clock);
      var readinessWriter = new TenantDatabaseRecoveryReadinessWriter(platform, clock);
      var connections = new TenantDatabaseVerificationConnectionFactory(
        Options.Create(storage), Options.Create(verification));
      var executor = new TenantDatabaseRestoreVerificationExecutor(
        registry,
        reads,
        runStore,
        new SqlServerTenantDatabaseRestoreVerificationProvider(
          connections,
          new TenantDatabaseBackupDestinationResolver(Options.Create(storage)),
          registry,
          Options.Create(verification),
          clock),
        new SqlServerRestoreVerificationProbe(connections),
        connections,
        readinessWriter,
        Options.Create(verification),
        clock);
      var reconciler = new TenantDatabaseRestoreVerificationReconciler(
        runStore,
        new SqlServerTenantDatabaseRestoreVerificationServerObserver(connections),
        Options.Create(verification),
        clock,
        NullLogger<TenantDatabaseRestoreVerificationReconciler>.Instance);
      var readinessRefresher = new TenantDatabaseRecoveryReadinessRefresher(
        registry, reads, fleet, readinessWriter, clock);
      var scheduler = new TenantDatabaseRestoreVerificationScheduler(
        fleet,
        runStore,
        executor,
        reconciler,
        readinessRefresher,
        Options.Create(verification),
        clock,
        NullLogger<TenantDatabaseRestoreVerificationScheduler>.Instance);

      return await scheduler.RunSweepAsync();
    }

    public async Task<PersistedOutcome> ReadPersistedOutcomeAsync(
      long verificationRunId,
      long backupRunId)
    {
      await using var platform = PlatformContext();
      var run = await platform.TenantDatabaseRestoreVerificationRuns.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == verificationRunId);
      var backup = await platform.TenantDatabaseBackupRuns.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == backupRunId);
      var database = await platform.TenantDatabases.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == TenantDatabaseId);
      return new PersistedOutcome(
        run.Status,
        run.CompletedUtc,
        backup.VerificationState,
        database.LastRestoreVerificationUtc);
    }

    public async Task<SchedulerPersistedOutcome> ReadSchedulerPersistedOutcomeAsync(long backupRunId)
    {
      await using var platform = PlatformContext();
      var run = await platform.TenantDatabaseRestoreVerificationRuns.AsNoTracking()
        .SingleAsync(candidate => candidate.TenantDatabaseId == TenantDatabaseId);
      verificationCatalog = run.VerificationDatabaseName;
      var backup = await platform.TenantDatabaseBackupRuns.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == backupRunId);
      var database = await platform.TenantDatabases.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == TenantDatabaseId);
      return new SchedulerPersistedOutcome(
        run.Id,
        run.Status,
        run.TenantDatabaseId,
        run.SourceBackupRunId,
        run.Depth,
        run.RestoreServerKey,
        run.VerificationDatabaseName,
        run.CompletedUtc,
        backup.VerificationState,
        database.LastRestoreVerificationUtc,
        database.RecoveryReadinessStatus);
    }

    private PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(
          ConnectionFor(platformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock(now));
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { verificationCatalog, sourceCatalog, platformCatalog }
        .Where(value => !string.IsNullOrWhiteSpace(value)))
      {
        try
        {
          await ExecuteSqlAsync("master",
            $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{catalog}]; END");
        }
        catch (SqlException)
        {
        }
      }

      if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
      {
        try { Directory.Delete(root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
      }
    }

    private static string TestRoot() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_BACKUP_ROOT") ??
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SSAS_BackupTests");

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }.ConnectionString;

    private static async Task ExecuteSqlAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 600;
      await command.ExecuteNonQueryAsync();
    }
  }

  private sealed record PersistedOutcome(
    TenantDatabaseRestoreVerificationStatus RunStatus,
    DateTimeOffset? RunCompletedUtc,
    TenantDatabaseBackupVerificationState BackupVerificationState,
    DateTimeOffset? LastRestoreVerificationUtc);

  private sealed record SchedulerPersistedOutcome(
    long VerificationRunId,
    TenantDatabaseRestoreVerificationStatus RunStatus,
    long TenantDatabaseId,
    long SourceBackupRunId,
    TenantDatabaseRestoreDepth RequestedDepth,
    string RestoreServerKey,
    string? VerificationDatabaseName,
    DateTimeOffset? RunCompletedUtc,
    TenantDatabaseBackupVerificationState BackupVerificationState,
    DateTimeOffset? LastRestoreVerificationUtc,
    TenantDatabaseRecoveryReadinessStatus RecoveryReadinessStatus);

  private sealed class TestClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => utcNow;
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "d7-full-path-test";
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
}
