using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// THE RECOVERY-GATED DEDICATED ACTIVATION MATRIX (ADR-017 cutover ordering, ADR-022 §18).
//
// The property under test throughout is that `Protected` and "actually restore-verified" are INDEPENDENT
// requirements. Phase D deliberately defined `Protected` as "every obligation the active policy requires is
// satisfied", which for a policy with no verification interval is reachable without any restore ever having
// happened. These tests pin that gap open — a database can be perfectly `Protected` and still refuse
// activation — because closing it by redefining `Protected` would re-grade the whole fleet.
public sealed class TenantDatabaseRecoveryActivationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

  // ---- THE TWO PASSING CASES -------------------------------------------------------------------------

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Protected_with_a_current_restore_verification_is_allowed()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(Inputs(), Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.Allowed, decision);
  }

  // A Level C policy verified at Level C. The depth comparison is ordered, so the deepest requirement is
  // satisfied only by a verification that actually reached it.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_log_policy_verified_at_log_depth_is_allowed()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(
        differentialMinutes: 1_440,
        logMinutes: 15,
        verifiedDepth: TenantDatabaseRestoreDepth.FullWithDifferentialAndLog),
      Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.Allowed, decision);
  }

  // ---- PROTECTED IS NOT SUFFICIENT -------------------------------------------------------------------

  // The case this gate exists for. The policy sets no verification interval, so `Protected` is legitimately
  // reached with no restore ever performed — and activation must still refuse.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Protected_without_any_restore_verification_is_refused()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(
        verificationIntervalDays: null,
        verificationRunId: null,
        verifiedBaseline: null,
        verifiedDepth: null,
        verifiedUtc: null,
        lastRestoreVerificationUtc: null),
      Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedNeverRestoreVerified, decision);
  }

  // A partial verification record is not evidence: without the depth it reached, nothing establishes that
  // the required recovery path was exercised.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_verification_record_missing_its_depth_is_refused()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(Inputs(verifiedDepth: null), Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedNeverRestoreVerified, decision);
  }

  // ---- EXACT EVIDENCE --------------------------------------------------------------------------------

  // THE SUPERSEDED-BASELINE REFUSAL. A newer full backup means the verified chain is no longer the one a
  // restore would take, however recent the verification itself was.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_verification_of_a_superseded_baseline_is_refused()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(currentBaseline: 205, verifiedBaseline: 100), Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedVerificationSupersededBaseline, decision);
  }

  // A Full-only verification does not license activating a database whose policy promises log recovery.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_verification_shallower_than_the_required_depth_is_refused()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(
        differentialMinutes: 1_440,
        logMinutes: 15,
        verifiedDepth: TenantDatabaseRestoreDepth.Full),
      Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedVerificationDepthInsufficient, decision);
  }

  // Freshness is measured against the EXACT RUN's completion, not the cached aggregate timestamp. Here the
  // aggregate still looks current and the run does not — the run wins.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_verification_run_aged_past_the_interval_is_refused_even_when_the_aggregate_looks_current()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(
        verificationIntervalDays: 30,
        verifiedUtc: Now.AddDays(-45),
        lastRestoreVerificationUtc: Now.AddDays(-1)),
      Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedVerificationStale, decision);
  }

  // A held `Protected` is a projection, and projections age. The one recheck that is pure arithmetic over
  // persisted facts still runs against the current clock.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_stale_protected_projection_is_refused_when_verification_is_now_overdue()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(
        verificationIntervalDays: 30,
        verifiedUtc: Now.AddDays(-60),
        lastRestoreVerificationUtc: Now.AddDays(-60)),
      Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedVerificationOverdue, decision);
  }

  // Protected, verified, but the run history holds no full backup. The projection and the history disagree,
  // and activation refuses rather than resolving it.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_protected_database_with_no_current_baseline_is_refused()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(Inputs(currentBaseline: null), Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedBaselineUnavailable, decision);
  }

  // ---- EVERY NON-PROTECTED READINESS VERDICT ---------------------------------------------------------

  [Theory]
  [InlineData(TenantDatabaseRecoveryReadinessStatus.Degraded,
    TenantDatabaseRecoveryActivationDecision.RefusedDegraded)]
  [InlineData(TenantDatabaseRecoveryReadinessStatus.Unprotected,
    TenantDatabaseRecoveryActivationDecision.RefusedUnprotected)]
  [InlineData(TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid,
    TenantDatabaseRecoveryActivationDecision.RefusedRecoveryModelInvalid)]
  [InlineData(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue,
    TenantDatabaseRecoveryActivationDecision.RefusedVerificationOverdue)]
  [InlineData(TenantDatabaseRecoveryReadinessStatus.Unknown,
    TenantDatabaseRecoveryActivationDecision.RefusedRecoveryReadinessUnknown)]
  [Trait("Decision", "ADR-022")]
  public void A_readiness_verdict_other_than_protected_refuses_activation(
    TenantDatabaseRecoveryReadinessStatus held,
    TenantDatabaseRecoveryActivationDecision expected)
  {
    // Every verification fact is impeccable; only the readiness verdict differs. Refusal must come from the
    // readiness half alone, so neither requirement can stand in for the other.
    var decision = TenantDatabaseRecoveryActivation.Decide(Inputs(held: held), Now);

    Assert.Equal(expected, decision);
  }

  // CustomerManaged reaches `Unknown` through the readiness evaluator and can never be activated on the
  // strength of a customer's assertion.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_customer_managed_database_is_refused()
  {
    var decision = TenantDatabaseRecoveryActivation.Decide(
      Inputs(
        hostingMode: TenantDatabaseHostingMode.CustomerManaged,
        held: TenantDatabaseRecoveryReadinessStatus.Unknown),
      Now);

    Assert.Equal(TenantDatabaseRecoveryActivationDecision.RefusedRecoveryReadinessUnknown, decision);
  }

  // ---- THE APPLICATION GATE --------------------------------------------------------------------------

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_gate_authorizes_a_protected_and_currently_verified_database()
  {
    var gate = Gate(Evidence());

    var authorized = await gate.AuthorizeActivationAsync(1);

    Assert.True(authorized.IsSuccess);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_gate_reports_the_exact_refusal_reason()
  {
    var gate = Gate(Evidence() with { CurrentBaselineBackupRunId = 999 });

    var authorized = await gate.AuthorizeActivationAsync(1);

    Assert.True(authorized.IsFailure);
    Assert.Equal(
      TenantStorageErrors.RecoveryActivationRestoreVerificationSuperseded.Code, authorized.Error.Code);
  }

  // Absent evidence is never an authorisation.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_gate_refuses_a_database_it_cannot_find()
  {
    var gate = Gate(evidence: null);

    var authorized = await gate.AuthorizeActivationAsync(1);

    Assert.True(authorized.IsFailure);
    Assert.Equal(TenantStorageErrors.RecoveryActivationEvidenceUnavailable.Code, authorized.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_gate_refuses_a_non_positive_database_identity_without_reading_evidence()
  {
    var reads = new FakeActivationReads(Evidence());
    var gate = new TenantDatabaseRecoveryActivationGate(reads, new FixedClock(Now));

    var authorized = await gate.AuthorizeActivationAsync(0);

    Assert.True(authorized.IsFailure);
    Assert.Equal(TenantStorageErrors.RecoveryActivationEvidenceUnavailable.Code, authorized.Error.Code);
    Assert.Equal(0, reads.Calls);
  }

  // ---- Fixtures --------------------------------------------------------------------------------------

  private static TenantDatabaseRecoveryActivationGate Gate(
    TenantDatabaseRecoveryActivationEvidence? evidence) =>
    new(new FakeActivationReads(evidence), new FixedClock(Now));

  private static TenantDatabaseRecoveryActivationEvidence Evidence() =>
    new(
      1,
      TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode.Dedicated,
      TenantDatabaseProvisioningStatus.Ready,
      PolicyExists: true,
      PolicyEnabled: true,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      FullBackupIntervalMinutes: 1_440,
      DifferentialBackupIntervalMinutes: null,
      TransactionLogBackupIntervalMinutes: null,
      RestoreVerificationIntervalDays: 30,
      MaximumBackupAgeMinutes: 2_880,
      TenantDatabaseRecoveryReadinessStatus.Protected,
      LastSuccessfulFullBackupUtc: Now.AddHours(-1),
      LastSuccessfulDifferentialBackupUtc: null,
      LastSuccessfulLogBackupUtc: null,
      LastRestoreVerificationUtc: Now.AddDays(-1),
      CurrentBaselineBackupRunId: 100,
      VerifiedVerificationRunId: 7,
      VerifiedSourceBackupRunId: 100,
      TenantDatabaseRestoreDepth.Full,
      VerificationCompletedUtc: Now.AddDays(-1));

  private static TenantDatabaseRecoveryActivationInputs Inputs(
    TenantDatabaseRecoveryReadinessStatus held = TenantDatabaseRecoveryReadinessStatus.Protected,
    TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
    int? verificationIntervalDays = 30,
    int? differentialMinutes = null,
    int? logMinutes = null,
    long? currentBaseline = 100,
    long? verificationRunId = 7,
    long? verifiedBaseline = 100,
    TenantDatabaseRestoreDepth? verifiedDepth = TenantDatabaseRestoreDepth.Full,
    DateTimeOffset? verifiedUtc = null,
    DateTimeOffset? lastRestoreVerificationUtc = null) =>
    new(
      new TenantDatabaseRecoveryReadinessInputs(
        hostingMode,
        PolicyExists: true,
        PolicyEnabled: true,
        TenantDatabaseBackupManagementMode.AutomaticByPlatform,
        FullBackupIntervalMinutes: 1_440,
        differentialMinutes,
        logMinutes,
        verificationIntervalDays,
        MaximumBackupAgeMinutes: 2_880,
        LastSuccessfulFullBackupUtc: Now.AddHours(-1),
        LastSuccessfulDifferentialBackupUtc: null,
        LastSuccessfulLogBackupUtc: null,
        lastRestoreVerificationUtc ?? verifiedUtc ?? Now.AddDays(-1),
        ObservedRecoveryModel: null,
        PlatformChainBreakDetected: false,
        HeldRecoveryReadinessStatus: held),
      currentBaseline,
      verificationRunId,
      verifiedBaseline,
      verifiedDepth,
      verifiedUtc ?? Now.AddDays(-1));

  private sealed class FakeActivationReads(TenantDatabaseRecoveryActivationEvidence? evidence)
    : ITenantDatabaseRecoveryActivationReadRepository
  {
    public int Calls { get; private set; }

    public Task<TenantDatabaseRecoveryActivationEvidence?> FindActivationEvidenceAsync(
      long tenantDatabaseId,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(evidence);
    }
  }

  private sealed class FixedClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => utcNow;
  }
}
