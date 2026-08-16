using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseRecoveryReadinessRefresherTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

  [Theory]
  [InlineData(null, null, TenantDatabaseRecoveryReadinessStatus.Protected)]
  [InlineData(30, 29.0, TenantDatabaseRecoveryReadinessStatus.Protected)]
  [InlineData(30, 30.0, TenantDatabaseRecoveryReadinessStatus.Protected)]
  [InlineData(30, 30.0000115741, TenantDatabaseRecoveryReadinessStatus.VerificationOverdue)]
  public async Task Refresh_pins_verification_interval_boundaries(
    int? intervalDays, double? verifiedAgeDays, TenantDatabaseRecoveryReadinessStatus expected)
  {
    var fixture = Fixture.Create(intervalDays, verifiedAgeDays is null ? null : Now.AddDays(-verifiedAgeDays.Value));

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(expected, fixture.Writer.Status);
  }

  [Fact]
  public async Task Never_verified_database_with_an_active_interval_is_verification_overdue()
  {
    var fixture = Fixture.Create(intervalDays: 30, durableVerificationUtc: null);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, fixture.Writer.Status);
  }

  [Fact]
  public async Task Held_degraded_without_new_recovery_evidence_remains_degraded()
  {
    var fixture = Fixture.Create(intervalDays: null, durableVerificationUtc: Now.AddDays(-1),
      held: TenantDatabaseRecoveryReadinessStatus.Degraded);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Writer.Status);
  }

  [Fact]
  public async Task Recovery_model_invalid_outranks_stale_verification()
  {
    var fixture = Fixture.Create(intervalDays: 30, durableVerificationUtc: Now.AddDays(-31),
      held: TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid,
      logIntervalMinutes: 30);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid, fixture.Writer.Status);
  }

  [Fact]
  public async Task Refresh_uses_durable_full_run_when_aggregate_full_timestamp_is_stale()
  {
    var durableFull = Now.AddHours(-1);
    var fixture = Fixture.Create(intervalDays: null, durableVerificationUtc: null,
      aggregateFullUtc: Now.AddDays(-500), durableFullUtc: durableFull);

    Assert.Equal(Now.AddDays(-500), fixture.BackupReads.Evidence.LastSuccessfulFullBackupUtc);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, fixture.Writer.Status);
    Assert.Equal(durableFull, fixture.Writer.FullUtc);
  }

  [Fact]
  public async Task Refresh_uses_durable_differential_run_when_aggregate_differential_is_missing()
  {
    var durableDifferential = Now.AddHours(-1);
    var fixture = Fixture.Create(intervalDays: null, durableVerificationUtc: null,
      differentialIntervalMinutes: 1_440,
      aggregateDifferentialUtc: null,
      durableDifferentialUtc: durableDifferential);

    Assert.Null(fixture.BackupReads.Evidence.LastSuccessfulDifferentialBackupUtc);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, fixture.Writer.Status);
    Assert.Equal(durableDifferential, fixture.Writer.DifferentialUtc);
  }

  [Fact]
  public async Task Refresh_uses_durable_log_run_when_aggregate_log_is_missing()
  {
    var durableLog = Now.AddMinutes(-5);
    var fixture = Fixture.Create(intervalDays: null, durableVerificationUtc: null,
      differentialIntervalMinutes: 1_440,
      logIntervalMinutes: 30,
      aggregateLogUtc: null,
      durableDifferentialUtc: Now.AddHours(-1),
      durableLogUtc: durableLog);

    Assert.Null(fixture.BackupReads.Evidence.LastSuccessfulLogBackupUtc);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Writer.Status);
    Assert.Equal(durableLog, fixture.Writer.LogUtc);
  }

  [Fact]
  public async Task Held_unprotected_chain_break_is_preserved_during_refresh()
  {
    var fixture = Fixture.Create(intervalDays: null, durableVerificationUtc: null,
      held: TenantDatabaseRecoveryReadinessStatus.Unprotected);

    await fixture.Refresher.RefreshAsync(1);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Writer.Status);
  }

  private sealed class Fixture
  {
    private Fixture(TestRegistry registry, TestBackupReads backupReads, TestVerificationReads verificationReads,
      TestWriter writer)
    {
      BackupReads = backupReads;
      Writer = writer;
      Refresher = new TenantDatabaseRecoveryReadinessRefresher(
        registry, backupReads, verificationReads, writer, new TestClock());
    }

    public TestWriter Writer { get; }
    public TestBackupReads BackupReads { get; }
    public TenantDatabaseRecoveryReadinessRefresher Refresher { get; }

    public static Fixture Create(int? intervalDays, DateTimeOffset? durableVerificationUtc,
      TenantDatabaseRecoveryReadinessStatus held = TenantDatabaseRecoveryReadinessStatus.Protected,
      int? differentialIntervalMinutes = null,
      int? logIntervalMinutes = null,
      DateTimeOffset? aggregateFullUtc = null,
      DateTimeOffset? aggregateDifferentialUtc = null,
      DateTimeOffset? aggregateLogUtc = null,
      DateTimeOffset? durableFullUtc = null,
      DateTimeOffset? durableDifferentialUtc = null,
      DateTimeOffset? durableLogUtc = null)
    {
      var evidence = new TenantDatabaseRecoveryEvidenceRecord(1, aggregateFullUtc ?? Now.AddDays(-1),
        aggregateDifferentialUtc, aggregateLogUtc,
        Now.AddDays(-500), held);
      return new Fixture(new TestRegistry(),
        new TestBackupReads(new TenantDatabaseBackupPolicyRecord(1, 1, true,
          TenantDatabaseBackupManagementMode.AutomaticByPlatform, "destination", 1_440,
          differentialIntervalMinutes,
          logIntervalMinutes, 30, intervalDays, 2_880,
          TenantDatabaseBackupCompressionMode.Disabled, TenantDatabaseBackupEncryptionMode.StorageManaged), evidence),
        new TestVerificationReads(new TenantDatabaseDurableRecoveryEvidence(
          1,
          durableFullUtc ?? Now.AddDays(-1),
          durableDifferentialUtc,
          durableLogUtc,
          durableVerificationUtc)),
        new TestWriter());
    }
  }

  private sealed class TestRegistry : ITenantDatabaseRegistryReadRepository
  {
    public Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantDatabaseAssignmentRecord?>(null);
    public Task<IReadOnlyList<TenantDatabaseDescriptor>> ListPhysicalDatabasesAsync(long afterId, int take,
      CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TenantDatabaseDescriptor>>(
      afterId == 0 ? [new TenantDatabaseDescriptor(1, "source", "tenant", TenantDatabaseHostingMode.PlatformManaged,
        TenantDatabaseStorageMode.Dedicated, TenantDatabaseProvisioningStatus.Ready,
        TenantDatabaseMigrationManagementMode.AutomaticByPlatform, TenantDatabaseConnectivityStatus.Healthy,
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, TenantDatabaseMigrationExecutionStatus.Idle, Now)] : []);
  }

  private sealed class TestBackupReads(TenantDatabaseBackupPolicyRecord policy, TenantDatabaseRecoveryEvidenceRecord evidence)
    : ITenantDatabaseBackupReadRepository
  {
    public TenantDatabaseRecoveryEvidenceRecord Evidence => evidence;
    public Task<TenantDatabaseBackupPolicyRecord?> FindPolicyAsync(long id, CancellationToken ct = default) => Task.FromResult<TenantDatabaseBackupPolicyRecord?>(policy);
    public Task<IReadOnlyList<TenantDatabaseBackupRunRecord>> ListRecentRunsAsync(long id, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TenantDatabaseBackupRunRecord>>([]);
    public Task<TenantDatabaseBackupRunRecord?> FindLatestSuccessfulRunAsync(long id, string provider, string code, CancellationToken ct = default) => Task.FromResult<TenantDatabaseBackupRunRecord?>(null);
    public Task<TenantDatabaseRecoveryEvidenceRecord?> FindRecoveryEvidenceAsync(long id, CancellationToken ct = default) => Task.FromResult<TenantDatabaseRecoveryEvidenceRecord?>(evidence);
    public Task<IReadOnlyList<SSAS.Platform.Domain.TenantStorage.TenantDatabaseBackupChainCandidate>> ListChainCandidatesAsync(long id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SSAS.Platform.Domain.TenantStorage.TenantDatabaseBackupChainCandidate>>([]);
  }

  private sealed class TestVerificationReads(TenantDatabaseDurableRecoveryEvidence evidence)
    : ITenantDatabaseRestoreVerificationFleetReadRepository
  {
    public Task<IReadOnlyList<string>> ListEligibleSourceServerKeysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>> ListCandidatesAsync(string key, long after, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>>([]);
    public Task<TenantDatabaseDurableRecoveryEvidence?> FindDurableRecoveryEvidenceAsync(long id, CancellationToken ct = default) =>
      Task.FromResult<TenantDatabaseDurableRecoveryEvidence?>(evidence);
  }

  private sealed class TestWriter : ITenantDatabaseRecoveryReadinessWriter
  {
    public TenantDatabaseRecoveryReadinessStatus? Status { get; private set; }
    public DateTimeOffset? FullUtc { get; private set; }
    public DateTimeOffset? DifferentialUtc { get; private set; }
    public DateTimeOffset? LogUtc { get; private set; }
    public DateTimeOffset? VerificationUtc { get; private set; }
    public Task RecordRecoveryReadinessAsync(long id, TenantDatabaseRecoveryReadinessStatus status, string actor,
      DateTimeOffset? full = null, DateTimeOffset? diff = null, DateTimeOffset? log = null, DateTimeOffset? verification = null,
      CancellationToken ct = default)
    {
      Status = status;
      FullUtc = full;
      DifferentialUtc = diff;
      LogUtc = log;
      VerificationUtc = verification;
      return Task.CompletedTask;
    }
  }

  private sealed class TestClock : IDateTimeProvider { public DateTimeOffset UtcNow => Now; }
}
