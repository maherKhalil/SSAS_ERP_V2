using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

public sealed class TenantDatabaseRestoreVerificationExecutorTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Eligible_full_restore_and_all_probes_produce_restore_verified()
  {
    var fixture = Fixture.Full();

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsSuccess);
    Assert.True(result.Value.RestoreVerified);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, result.Value.Status);
    Assert.Equal(1, fixture.Provider.Calls);
    Assert.Equal(1, fixture.Probe.Calls);
    Assert.Equal(fixture.Run.SourceBackupRunId, fixture.Store.VerifiedSourceBackupRunId);
    Assert.Equal(Now, fixture.Readiness.LastRestoreVerificationUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Requested_differential_but_achieved_full_is_degraded_and_not_verified()
  {
    var fixture = Fixture.Differential();
    fixture.Provider.Result = Restored(TenantDatabaseRestoreDepth.Full);

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.False(result.Value.RestoreVerified);
    Assert.Equal(TenantDatabaseRestoreDepth.Full, result.Value.AchievedDepth);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Readiness.Status);
    Assert.Equal(0, fixture.Probe.Calls);
    Assert.Null(fixture.Store.VerifiedSourceBackupRunId);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Requested_log_but_achieved_differential_is_degraded_and_not_verified()
  {
    var fixture = Fixture.Log();
    fixture.Provider.Result = Restored(TenantDatabaseRestoreDepth.FullWithDifferential);

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.False(result.Value.RestoreVerified);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Readiness.Status);
    Assert.Equal(0, fixture.Probe.Calls);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Known_chain_break_does_not_call_provider_and_is_unprotected()
  {
    var fixture = Fixture.Differential();
    fixture.Reads.Candidates =
    [
      FullCandidate(checkpointLsn: 10),
      DifferentialCandidate(databaseBackupLsn: 999, lastLsn: 300)
    ];

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.False(result.Value.RestoreVerified);
    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Missing_checkpoint_metadata_preserves_held_degraded_without_calling_provider()
  {
    var fixture = Fixture.Differential();
    fixture.Reads.HeldReadinessStatus = TenantDatabaseRecoveryReadinessStatus.Degraded;
    fixture.Reads.Candidates = [FullCandidate(checkpointLsn: null)];

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, result.Value.Status);
    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Readiness.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Customer_managed_database_is_refused_before_provider()
  {
    var fixture = Fixture.Full();
    fixture.Registry.Database = fixture.Registry.Database! with
    {
      HostingMode = TenantDatabaseHostingMode.CustomerManaged
    };

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, result.Value.Status);
    Assert.Equal(0, fixture.Provider.Calls);
  }

  [Theory]
  [InlineData(TenantDatabaseBackupManagementMode.PlatformAfterApproval)]
  [InlineData(TenantDatabaseBackupManagementMode.CustomerDba)]
  public async Task Non_automatic_management_mode_is_refused_before_provider(
    TenantDatabaseBackupManagementMode mode)
  {
    var fixture = Fixture.Full();
    fixture.Reads.Policy = fixture.Reads.Policy! with { ManagementMode = mode };

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, fixture.Store.Status);
  }

  [Fact]
  public async Task Policy_drift_after_cas_is_rechecked_immediately_before_provider()
  {
    var fixture = Fixture.Full();
    fixture.Reads.PolicyAfterFirstRead = fixture.Reads.Policy! with { Enabled = false };

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsSuccess);
    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, fixture.Store.Status);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Missing_or_disabled_policy_is_refused_before_provider(bool removePolicy)
  {
    var fixture = Fixture.Full();
    fixture.Reads.Policy = removePolicy ? null : fixture.Reads.Policy! with { Enabled = false };

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, fixture.Store.Status);
  }

  [Fact]
  public async Task Missing_physical_database_row_is_controlled_and_never_calls_provider()
  {
    var fixture = Fixture.Full();
    fixture.Registry.Database = null;

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsSuccess);
    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantStorageErrors.TenantDatabaseRequired.Code, result.Value.SafeErrorSummary);
  }

  [Fact]
  public async Task Wrong_exact_run_is_rejected_before_provider()
  {
    var fixture = Fixture.Full();

    var result = await fixture.Executor.ExecuteAsync(
      fixture.Run.TenantDatabaseId, fixture.Run.VerificationRunId + 1, fixture.Run.Depth);

    Assert.True(result.IsFailure);
    Assert.Equal(0, fixture.Provider.Calls);
  }

  [Theory]
  [InlineData(TenantDatabaseRestoreVerificationStatus.Restoring)]
  [InlineData(TenantDatabaseRestoreVerificationStatus.Succeeded)]
  public async Task Run_that_already_moved_cannot_be_replayed(
    TenantDatabaseRestoreVerificationStatus status)
  {
    var fixture = Fixture.Full(status);

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsFailure);
    Assert.Equal(0, fixture.Provider.Calls);
  }

  [Fact]
  public async Task Losing_the_admitted_to_restoring_compare_and_set_never_calls_provider()
  {
    var fixture = Fixture.Full();
    fixture.Store.LoseBeginCompareAndSet = true;

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsFailure);
    Assert.Equal(0, fixture.Provider.Calls);
  }

  [Fact]
  public async Task Target_that_is_no_longer_isolated_never_calls_provider()
  {
    var fixture = Fixture.Full();
    fixture.Connections.Refuse = TenantStorageErrors.RestoreVerificationTargetNotIsolated;

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, fixture.Store.Status);
  }

  [Fact]
  public async Task Stale_source_baseline_never_calls_provider()
  {
    var fixture = Fixture.Full();
    fixture.Reads.Candidates = [FullCandidate(backupRunId: 999)];

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantStorageErrors.RestoreVerificationTargetDrifted.Code, result.Value.SafeErrorSummary);
    Assert.Equal(0, fixture.Provider.Calls);
  }

  [Theory]
  [InlineData("migration-history")]
  [InlineData("application-model")]
  public async Task Post_restore_probe_failure_is_unprotected_and_never_verified(string reason)
  {
    var fixture = Fixture.Full();
    fixture.Probe.Result = TenantDatabaseRestoreProbeResult.Failed(reason);

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.False(result.Value.RestoreVerified);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
    Assert.Null(fixture.Store.VerifiedSourceBackupRunId);
  }

  [Fact]
  public async Task Success_records_exact_source_backup_before_aggregate_timestamp()
  {
    var fixture = Fixture.Full();

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(fixture.Run.SourceBackupRunId, fixture.Store.VerifiedSourceBackupRunId);
    Assert.True(fixture.Events.IndexOf("run-succeeded-and-source-verified") <
      fixture.Events.IndexOf("aggregate-timestamp"));
    Assert.Equal(Now, fixture.Store.CompletedUtc);
    Assert.Equal(Now, fixture.Readiness.LastRestoreVerificationUtc);
  }

  [Fact]
  public async Task Infrastructure_failure_preserves_held_simple_recovery_model_evidence()
  {
    var fixture = Fixture.Log();
    fixture.Reads.HeldReadinessStatus = TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid;
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable,
      SafeErrorSummary: "host-unavailable");

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, result.Value.Status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid, fixture.Readiness.Status);
    Assert.Null(fixture.Readiness.LastRestoreVerificationUtc);
  }

  [Fact]
  public async Task Infrastructure_failure_does_not_promote_held_unprotected_chain_evidence()
  {
    var fixture = Fixture.Full();
    fixture.Reads.HeldReadinessStatus = TenantDatabaseRecoveryReadinessStatus.Unprotected;
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable,
      SafeErrorSummary: "host-unavailable");

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
  }

  [Fact]
  public async Task Infrastructure_failure_preserves_held_degraded_despite_fresh_positive_timestamps()
  {
    var fixture = Fixture.Differential();
    fixture.Reads.HeldReadinessStatus = TenantDatabaseRecoveryReadinessStatus.Degraded;
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable,
      SafeErrorSummary: "host-unavailable");

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, result.Value.Status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Readiness.Status);
  }

  [Fact]
  public async Task Successful_new_verification_can_promote_prior_degraded_to_protected()
  {
    var fixture = Fixture.Differential();
    fixture.Reads.HeldReadinessStatus = TenantDatabaseRecoveryReadinessStatus.Degraded;

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.True(result.Value.RestoreVerified);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, result.Value.Status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, fixture.Readiness.Status);
  }

  [Theory]
  [InlineData(TenantDatabaseRestoreDepth.Full)]
  [InlineData(TenantDatabaseRestoreDepth.FullWithDifferential)]
  public async Task A_required_artifact_that_is_unavailable_is_a_hard_chain_break(
    TenantDatabaseRestoreDepth depth)
  {
    var fixture = depth == TenantDatabaseRestoreDepth.Full ? Fixture.Full() : Fixture.Differential();
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.ArtifactUnavailable,
      SafeErrorSummary: TenantStorageErrors.RestoreVerificationArtifactUnavailable.Code);

    var result = await fixture.ExecuteAsync(depth);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Failed, result.Value.Status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
    Assert.Null(fixture.Store.VerifiedSourceBackupRunId);
  }

  [Fact]
  public async Task A_topology_precondition_is_not_interpreted_as_an_artifact_failure()
  {
    var fixture = Fixture.Log();
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.BlockedByPrecondition,
      SafeErrorSummary: TenantStorageErrors.RestoreVerificationTargetAlreadyExists.Code);

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, result.Value.Status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Readiness.Status);
  }

  [Fact]
  public async Task Policy_removed_after_cas_is_current_for_readiness_and_refuses_execution()
  {
    var fixture = Fixture.Full();
    fixture.Reads.PolicyReadSequence = [fixture.Reads.Policy, null, null];

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(0, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, result.Value.Status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
  }

  [Fact]
  public async Task Policy_changed_during_execution_is_current_for_readiness()
  {
    var fixture = Fixture.Full();
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable,
      SafeErrorSummary: "transport");
    fixture.Reads.PolicyReadSequence =
    [
      fixture.Reads.Policy,
      fixture.Reads.Policy,
      fixture.Reads.Policy! with { ManagementMode = TenantDatabaseBackupManagementMode.CustomerDba }
    ];

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(1, fixture.Provider.Calls);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
  }

  [Theory]
  [InlineData(TenantDatabaseRecoveryModel.Simple, TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid)]
  [InlineData(TenantDatabaseRecoveryModel.Full, TenantDatabaseRecoveryReadinessStatus.Protected)]
  [InlineData(TenantDatabaseRecoveryModel.BulkLogged, TenantDatabaseRecoveryReadinessStatus.Protected)]
  public async Task Successful_log_verification_uses_the_observed_recovery_model(
    TenantDatabaseRecoveryModel model,
    TenantDatabaseRecoveryReadinessStatus expected)
  {
    var fixture = Fixture.Log();
    fixture.Probe.Result = TenantDatabaseRestoreProbeResult.Succeeded(model, "latest");

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(expected, fixture.Readiness.Status);
  }

  [Fact]
  public async Task Baseline_restore_failure_is_unprotected()
  {
    var fixture = Fixture.Full();
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.RestoreFailed,
      RestoredStepCount: 0,
      SafeErrorSummary: "full-failed");

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unprotected, fixture.Readiness.Status);
  }

  [Fact]
  public async Task Failure_after_full_restore_is_degraded()
  {
    var fixture = Fixture.Differential();
    fixture.Provider.Result = new TenantDatabaseRestoreVerificationResult(
      TenantDatabaseRestoreVerificationOutcome.RestoreFailed,
      RestoredStepCount: 1,
      AchievedDepth: TenantDatabaseRestoreDepth.Full,
      SafeErrorSummary: "differential-failed");

    await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, fixture.Readiness.Status);
  }

  [Theory]
  [InlineData("provider")]
  [InlineData("probe")]
  [InlineData("chain-read")]
  [InlineData("success-persistence")]
  public async Task Handled_execution_exception_does_not_leave_run_restoring(string failurePoint)
  {
    var fixture = Fixture.Full();
    switch (failurePoint)
    {
      case "provider": fixture.Provider.Throw = true; break;
      case "probe": fixture.Probe.Throw = true; break;
      case "chain-read": fixture.Reads.ThrowOnCandidates = true; break;
      case "success-persistence": fixture.Store.ThrowOnSuccess = true; break;
    }

    var result = await fixture.ExecuteAsync(TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, fixture.Store.Status);
    Assert.NotEqual(TenantDatabaseRestoreVerificationStatus.Restoring, fixture.Store.Status);
  }

  private static TenantDatabaseRestoreVerificationResult Restored(TenantDatabaseRestoreDepth depth) =>
    new(
      TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline,
      VerificationDatabaseName: TenantDatabaseVerificationNaming.ForRun(1, 10),
      RestoredStepCount: (int)depth,
      AchievedDepth: depth,
      StartedUtc: Now.AddMinutes(-5),
      CompletedUtc: Now.AddMinutes(-1));

  private static TenantDatabaseBackupChainCandidate FullCandidate(
    long backupRunId = 101,
    decimal? checkpointLsn = 10) =>
    new(backupRunId, TenantDatabaseRestoreStepKind.Full, "backup", "full.bak",
      checkpointLsn, 10, 1, 100);

  private static TenantDatabaseBackupChainCandidate DifferentialCandidate(
    decimal databaseBackupLsn = 10,
    decimal lastLsn = 200) =>
    new(102, TenantDatabaseRestoreStepKind.Differential, "backup", "diff.bak",
      CheckpointLsn: 20, databaseBackupLsn, 90, lastLsn);

  private sealed class Fixture
  {
    private Fixture(TenantDatabaseRestoreDepth depth, TenantDatabaseRestoreVerificationStatus status)
    {
      Events = [];
      Run = new TenantDatabaseRestoreVerificationRunRecord(
        10, 1, 101, depth, "verify", status, null, Now.AddMinutes(-10), null);
      Store = new FakeRunStore(Run, Events);
      Registry = new FakeRegistry();
      Reads = new FakeBackupReads(depth);
      Provider = new FakeProvider { Result = Restored(depth) };
      Probe = new FakeProbe();
      Connections = new FakeConnections();
      Readiness = new FakeReadinessWriter(Events);
      Executor = new TenantDatabaseRestoreVerificationExecutor(
        Registry,
        Reads,
        Store,
        Provider,
        Probe,
        Connections,
        Readiness,
        Options.Create(new TenantDatabaseRestoreVerificationOptions
        {
          Enabled = true,
          RestoreServerKey = "verify",
          RestoreDataRoot = "D:\\verify",
          RestoreLogRoot = "L:\\verify"
        }),
        new FakeClock());
    }

    public static Fixture Full(
      TenantDatabaseRestoreVerificationStatus status = TenantDatabaseRestoreVerificationStatus.Admitted) =>
      new(TenantDatabaseRestoreDepth.Full, status);

    public static Fixture Differential() => new(TenantDatabaseRestoreDepth.FullWithDifferential,
      TenantDatabaseRestoreVerificationStatus.Admitted);

    public static Fixture Log() => new(TenantDatabaseRestoreDepth.FullWithDifferentialAndLog,
      TenantDatabaseRestoreVerificationStatus.Admitted);

    public List<string> Events { get; }
    public TenantDatabaseRestoreVerificationRunRecord Run { get; }
    public FakeRunStore Store { get; }
    public FakeRegistry Registry { get; }
    public FakeBackupReads Reads { get; }
    public FakeProvider Provider { get; }
    public FakeProbe Probe { get; }
    public FakeConnections Connections { get; }
    public FakeReadinessWriter Readiness { get; }
    public TenantDatabaseRestoreVerificationExecutor Executor { get; }

    public Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> ExecuteAsync(
      TenantDatabaseRestoreDepth depth) => Executor.ExecuteAsync(1, 10, depth);
  }

  private sealed class FakeRegistry : ITenantDatabaseRegistryReadRepository
  {
    public TenantDatabaseDescriptor? Database { get; set; } = new(
      1,
      "source",
      "SSAS_Shared_01",
      TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode.Shared,
      TenantDatabaseProvisioningStatus.Ready,
      TenantDatabaseMigrationManagementMode.AutomaticByPlatform,
      TenantDatabaseConnectivityStatus.Healthy,
      TenantDatabaseSchemaCompatibilityStatus.UpToDate,
      TenantDatabaseMigrationExecutionStatus.Succeeded,
      Now);

    public Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(
      Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<TenantDatabaseAssignmentRecord?>(null);

    public Task<IReadOnlyList<TenantDatabaseDescriptor>> ListPhysicalDatabasesAsync(
      long afterId, int take, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantDatabaseDescriptor>>(Database is null ? [] : [Database]);
  }

  private sealed class FakeBackupReads(TenantDatabaseRestoreDepth depth) : ITenantDatabaseBackupReadRepository
  {
    private int policyReads;

    public TenantDatabaseBackupPolicyRecord? Policy { get; set; } = new(
      1,
      1,
      Enabled: true,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      "backup",
      FullBackupIntervalMinutes: 10_080,
      DifferentialBackupIntervalMinutes: depth >= TenantDatabaseRestoreDepth.FullWithDifferential ? 1_440 : null,
      TransactionLogBackupIntervalMinutes: depth >= TenantDatabaseRestoreDepth.FullWithDifferentialAndLog ? 15 : null,
      RetentionExpectationDays: 30,
      RestoreVerificationIntervalDays: 30,
      MaximumBackupAgeMinutes: 20_000,
      TenantDatabaseBackupCompressionMode.PreferredWhereSupported,
      TenantDatabaseBackupEncryptionMode.StorageManaged);

    public TenantDatabaseBackupPolicyRecord? PolicyAfterFirstRead { get; set; }

    public IReadOnlyList<TenantDatabaseBackupPolicyRecord?>? PolicyReadSequence { get; set; }

    public TenantDatabaseRecoveryReadinessStatus HeldReadinessStatus { get; set; } =
      TenantDatabaseRecoveryReadinessStatus.Unknown;

    public IReadOnlyList<TenantDatabaseBackupChainCandidate> Candidates { get; set; } = depth switch
    {
      TenantDatabaseRestoreDepth.FullWithDifferential => [FullCandidate(), DifferentialCandidate()],
      TenantDatabaseRestoreDepth.FullWithDifferentialAndLog =>
      [
        FullCandidate(),
        DifferentialCandidate(),
        new TenantDatabaseBackupChainCandidate(
          103, TenantDatabaseRestoreStepKind.Log, "backup", "log.trn", 30, 10, 190, 250)
      ],
      _ => [FullCandidate()]
    };

    public bool ThrowOnCandidates { get; set; }

    public Task<TenantDatabaseBackupPolicyRecord?> FindPolicyAsync(
      long tenantDatabaseId, CancellationToken cancellationToken = default)
    {
      policyReads++;
      if (PolicyReadSequence is { Count: > 0 } sequence)
      {
        return Task.FromResult(sequence[Math.Min(policyReads - 1, sequence.Count - 1)]);
      }

      return Task.FromResult(policyReads > 1 && PolicyAfterFirstRead is not null
        ? PolicyAfterFirstRead
        : Policy);
    }

    public Task<IReadOnlyList<TenantDatabaseBackupRunRecord>> ListRecentRunsAsync(
      long tenantDatabaseId, int take, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantDatabaseBackupRunRecord>>([]);

    public Task<TenantDatabaseBackupRunRecord?> FindLatestSuccessfulRunAsync(
      long tenantDatabaseId, string operationProviderKey, string operationCode,
      CancellationToken cancellationToken = default) => Task.FromResult<TenantDatabaseBackupRunRecord?>(null);

    public Task<TenantDatabaseRecoveryEvidenceRecord?> FindRecoveryEvidenceAsync(
      long tenantDatabaseId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantDatabaseRecoveryEvidenceRecord?>(new(
        1,
        Now.AddHours(-1),
        depth >= TenantDatabaseRestoreDepth.FullWithDifferential ? Now.AddMinutes(-30) : null,
        depth >= TenantDatabaseRestoreDepth.FullWithDifferentialAndLog ? Now.AddMinutes(-5) : null,
        Now.AddDays(-1),
        HeldReadinessStatus));

    public Task<IReadOnlyList<TenantDatabaseBackupChainCandidate>> ListChainCandidatesAsync(
      long tenantDatabaseId, CancellationToken cancellationToken = default) =>
      ThrowOnCandidates
        ? Task.FromException<IReadOnlyList<TenantDatabaseBackupChainCandidate>>(new InvalidOperationException())
        : Task.FromResult(Candidates);
  }

  private sealed class FakeRunStore(
    TenantDatabaseRestoreVerificationRunRecord run,
    List<string> events) : ITenantDatabaseRestoreVerificationRunStore
  {
    public TenantDatabaseRestoreVerificationStatus Status { get; private set; } = run.Status;
    public bool LoseBeginCompareAndSet { get; set; }
    public bool ThrowOnSuccess { get; set; }
    public long? VerifiedSourceBackupRunId { get; private set; }
    public DateTimeOffset? CompletedUtc { get; private set; }
    public string? VerificationDatabaseName { get; private set; } = run.VerificationDatabaseName;

    public Task<TenantDatabaseRestoreVerificationRunRecord?> FindAsync(
      long verificationRunId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantDatabaseRestoreVerificationRunRecord?>(
        verificationRunId == run.VerificationRunId
          ? run with { Status = Status, VerificationDatabaseName = VerificationDatabaseName }
          : null);

    public Task<Result<long>> TryAdmitAsync(
      TenantDatabaseRestoreVerificationAdmissionRequest request,
      CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(run.VerificationRunId));

    public Task<Result> BeginRestoreAsync(
      long verificationRunId, string verificationDatabaseName, string actor,
      CancellationToken cancellationToken = default)
    {
      if (LoseBeginCompareAndSet || Status != TenantDatabaseRestoreVerificationStatus.Admitted)
      {
        return Task.FromResult(Result.Failure(TenantStorageErrors.RestoreVerificationNotAdmitted));
      }

      Status = TenantDatabaseRestoreVerificationStatus.Restoring;
      VerificationDatabaseName = verificationDatabaseName;
      events.Add("restoring");
      return Task.FromResult(Result.Success());
    }

    public Task<Result> MarkSucceededAsync(
      long verificationRunId, string actor, CancellationToken cancellationToken = default)
    {
      Status = TenantDatabaseRestoreVerificationStatus.Succeeded;
      return Task.FromResult(Result.Success());
    }

    public Task<Result<DateTimeOffset>> MarkSucceededAndRecordEvidenceAsync(
      long verificationRunId, long sourceBackupRunId, string actor,
      CancellationToken cancellationToken = default)
    {
      if (ThrowOnSuccess)
      {
        throw new InvalidOperationException();
      }

      Status = TenantDatabaseRestoreVerificationStatus.Succeeded;
      VerifiedSourceBackupRunId = sourceBackupRunId;
      CompletedUtc = Now;
      events.Add("run-succeeded-and-source-verified");
      return Task.FromResult(Result.Success(Now));
    }

    public Task<Result> MarkFailedAsync(
      long verificationRunId, string? errorSummary, string actor,
      CancellationToken cancellationToken = default)
    {
      Status = TenantDatabaseRestoreVerificationStatus.Failed;
      events.Add("failed");
      return Task.FromResult(Result.Success());
    }

    public Task<Result> MarkInfrastructureUnavailableAsync(
      long verificationRunId, string? reasonSummary, string actor,
      CancellationToken cancellationToken = default)
    {
      if (Status != TenantDatabaseRestoreVerificationStatus.Succeeded)
      {
        Status = TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable;
      }
      events.Add("infrastructure-unavailable");
      return Task.FromResult(Result.Success());
    }

    public Task<Result> RecordCleanupAsync(
      long verificationRunId, TenantDatabaseVerificationCleanupState state, string? errorSummary,
      string actor, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
  }

  private sealed class FakeProvider : ITenantDatabaseRestoreVerificationProvider
  {
    public int Calls { get; private set; }
    public bool Throw { get; set; }
    public TenantDatabaseRestoreVerificationResult Result { get; set; } = null!;

    public Task<TenantDatabaseRestoreVerificationResult> ExecuteAsync(
      TenantDatabaseRestoreVerificationRequest request,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      if (Throw)
      {
        throw new InvalidOperationException();
      }

      return Task.FromResult(Result with { VerificationDatabaseName = request.VerificationDatabaseName });
    }
  }

  private sealed class FakeProbe : ITenantDatabaseRestoreVerificationProbe
  {
    public int Calls { get; private set; }
    public bool Throw { get; set; }
    public TenantDatabaseRestoreProbeResult Result { get; set; } =
      TenantDatabaseRestoreProbeResult.Succeeded(TenantDatabaseRecoveryModel.Full, "latest");

    public Task<TenantDatabaseRestoreProbeResult> ExecuteAsync(
      TenantDatabaseRestoreProbeRequest request,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      if (Throw)
      {
        throw new InvalidOperationException();
      }
      return Task.FromResult(Result);
    }
  }

  private sealed class FakeConnections : ITenantDatabaseVerificationConnectionFactory
  {
    public Error? Refuse { get; set; }

    public Result<SqlConnection> Create(TenantDatabaseVerificationTarget target) =>
      Refuse is null
        ? Result.Success(new SqlConnection())
        : Result.Failure<SqlConnection>(Refuse);

    public Result<SqlConnection> CreateForVerificationDatabase(
      TenantDatabaseVerificationTarget target, string verificationDatabaseName) => Create(target);
  }

  private sealed class FakeReadinessWriter(List<string> events) : ITenantDatabaseRecoveryReadinessWriter
  {
    public TenantDatabaseRecoveryReadinessStatus? Status { get; private set; }
    public DateTimeOffset? LastRestoreVerificationUtc { get; private set; }

    public Task RecordRecoveryReadinessAsync(
      long tenantDatabaseId,
      TenantDatabaseRecoveryReadinessStatus status,
      string actor,
      DateTimeOffset? lastSuccessfulFullBackupUtc = null,
      DateTimeOffset? lastSuccessfulDifferentialBackupUtc = null,
      DateTimeOffset? lastSuccessfulLogBackupUtc = null,
      DateTimeOffset? lastRestoreVerificationUtc = null,
      CancellationToken cancellationToken = default)
    {
      Status = status;
      LastRestoreVerificationUtc = lastRestoreVerificationUtc;
      events.Add(lastRestoreVerificationUtc is null ? "readiness" : "aggregate-timestamp");
      return Task.CompletedTask;
    }
  }

  private sealed class FakeClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
