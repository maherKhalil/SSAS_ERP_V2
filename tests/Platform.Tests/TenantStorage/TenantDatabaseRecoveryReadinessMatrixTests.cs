using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// The recovery-readiness matrix (ADR-022 §6 and §17, v1.2).
//
// The evaluator is pure, so every one of these runs without a database, a clock or a provider — which is
// the point of having put the decision in the domain rather than in the provider that produces evidence.
[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseRecoveryReadinessMatrixTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public void A_platform_managed_database_meeting_every_policy_obligation_is_protected()
  {
    var status = TenantDatabaseRecoveryReadinessEvaluator.Evaluate(Healthy(), Now);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, status);
  }

  // D-4, and the most contestable line in v1.2: where the policy asks for no periodic restore verification,
  // a database may be Protected without ever having been restore-tested. `Protected` means the ACTIVE
  // POLICY'S obligations are satisfied — restore-provenness is observed separately.
  [Fact]
  public void A_database_whose_policy_requires_no_periodic_verification_reaches_protected_without_a_restore()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = null,
      LastRestoreVerificationUtc = null
    };

    Assert.False(TenantDatabaseRecoveryReadinessEvaluator.IsVerificationOverdue(inputs, Now));
    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Protected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  // A null interval means NO PERIODIC OBLIGATION — not a zero interval, not immediately overdue.
  [Fact]
  public void A_null_verification_interval_is_never_overdue_however_old_the_evidence()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = null,
      LastRestoreVerificationUtc = Now.AddYears(-5)
    };

    Assert.False(TenantDatabaseRecoveryReadinessEvaluator.IsVerificationOverdue(inputs, Now));
  }

  [Fact]
  public void Verification_required_and_never_performed_is_overdue()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = 30,
      LastRestoreVerificationUtc = null
    };

    Assert.True(TenantDatabaseRecoveryReadinessEvaluator.IsVerificationOverdue(inputs, Now));
    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.VerificationOverdue,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void Verification_required_and_stale_is_overdue()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = 30,
      LastRestoreVerificationUtc = Now.AddDays(-31)
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.VerificationOverdue,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void Verification_inside_its_interval_is_not_overdue()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = 30,
      LastRestoreVerificationUtc = Now.AddDays(-29)
    };

    Assert.False(TenantDatabaseRecoveryReadinessEvaluator.IsVerificationOverdue(inputs, Now));
  }

  [Fact]
  public void A_database_with_no_full_baseline_is_unprotected()
  {
    var inputs = Healthy() with { LastSuccessfulFullBackupUtc = null };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Unprotected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void A_customer_managed_database_is_unknown_and_never_protected()
  {
    var inputs = Healthy() with { HostingMode = TenantDatabaseHostingMode.CustomerManaged };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Unknown,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Theory]
  [InlineData(TenantDatabaseBackupManagementMode.CustomerDba)]
  [InlineData(TenantDatabaseBackupManagementMode.PlatformAfterApproval)]
  public void A_policy_the_platform_may_not_execute_is_unprotected(TenantDatabaseBackupManagementMode mode)
  {
    var inputs = Healthy() with { ManagementMode = mode };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Unprotected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void A_disabled_policy_is_unprotected()
  {
    var inputs = Healthy() with { PolicyEnabled = false };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Unprotected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  // ---- Recovery model (ADR-022 §9, v1.2).

  [Fact]
  public void A_log_policy_on_a_simple_recovery_database_is_recovery_model_invalid()
  {
    var inputs = Healthy() with
    {
      TransactionLogBackupIntervalMinutes = 15,
      LastSuccessfulLogBackupUtc = Now.AddMinutes(-5),
      ObservedRecoveryModel = TenantDatabaseRecoveryModel.Simple
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void A_full_only_policy_on_a_simple_recovery_database_is_valid()
  {
    var inputs = Healthy() with
    {
      TransactionLogBackupIntervalMinutes = null,
      DifferentialBackupIntervalMinutes = null,
      LastSuccessfulLogBackupUtc = null,
      LastSuccessfulDifferentialBackupUtc = null,
      ObservedRecoveryModel = TenantDatabaseRecoveryModel.Simple
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Protected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  // BULK_LOGGED supports the log chain, so a log policy on one is NOT invalid (ADR-022 §9, v1.2). The
  // point-in-time caveat inside minimally logged intervals is a documented limit on what Phase D claims,
  // not a readiness verdict.
  [Theory]
  [InlineData(TenantDatabaseRecoveryModel.Full)]
  [InlineData(TenantDatabaseRecoveryModel.BulkLogged)]
  public void A_log_policy_is_valid_on_full_and_bulk_logged(TenantDatabaseRecoveryModel model)
  {
    var inputs = Healthy() with
    {
      TransactionLogBackupIntervalMinutes = 15,
      LastSuccessfulLogBackupUtc = Now.AddMinutes(-5),
      ObservedRecoveryModel = model
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Protected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void A_log_policy_with_no_observed_recovery_model_is_degraded_not_protected()
  {
    var inputs = Healthy() with
    {
      TransactionLogBackupIntervalMinutes = 15,
      LastSuccessfulLogBackupUtc = Now.AddMinutes(-5),
      ObservedRecoveryModel = null
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Degraded,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  // ---- Degradation.

  [Fact]
  public void An_overdue_scheduled_backup_type_degrades_readiness()
  {
    var inputs = Healthy() with { LastSuccessfulFullBackupUtc = Now.AddDays(-30) };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Degraded,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  [Fact]
  public void A_recovery_point_older_than_the_policy_maximum_degrades_readiness()
  {
    var inputs = Healthy() with
    {
      MaximumBackupAgeMinutes = 60,
      LastSuccessfulFullBackupUtc = Now.AddMinutes(-120),
      LastSuccessfulDifferentialBackupUtc = null,
      LastSuccessfulLogBackupUtc = null,
      DifferentialBackupIntervalMinutes = null,
      TransactionLogBackupIntervalMinutes = null
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Degraded,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  // A known required-chain break OUTRANKS the depth gradation: it is not a slipping chain, it is one that
  // cannot be reconstructed (ADR-022 §17, v1.2).
  [Fact]
  public void A_platform_chain_break_is_unprotected_not_degraded()
  {
    var inputs = Healthy() with { PlatformChainBreakDetected = true };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Unprotected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, Now));
  }

  // ---- The verification failure matrix (ADR-022 §17, v1.2).

  [Theory]
  [InlineData(TenantDatabaseVerificationFailure.BaselineRestoreFailed,
    TenantDatabaseRecoveryReadinessStatus.Unprotected)]
  [InlineData(TenantDatabaseVerificationFailure.DeeperChainRestoreFailed,
    TenantDatabaseRecoveryReadinessStatus.Degraded)]
  [InlineData(TenantDatabaseVerificationFailure.RequiredChainBreak,
    TenantDatabaseRecoveryReadinessStatus.Unprotected)]
  [InlineData(TenantDatabaseVerificationFailure.PostRestoreProbeFailed,
    TenantDatabaseRecoveryReadinessStatus.Unprotected)]
  public void Verification_failures_map_to_readiness_by_depth(
    TenantDatabaseVerificationFailure failure,
    TenantDatabaseRecoveryReadinessStatus expected)
  {
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
      failure, Healthy(), Now);

    Assert.Equal(expected, status);
  }

  // CLEANUP-ONLY FAILURE LEAVES READINESS ALONE. Recoverability was proven before the drop failed, and that
  // does not become untrue afterwards. Null means "do not write", not "no opinion".
  [Fact]
  public void A_cleanup_failure_does_not_change_readiness()
  {
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
      TenantDatabaseVerificationFailure.CleanupFailed, Healthy(), Now);

    Assert.Null(status);
  }

  // INFRASTRUCTURE FAILURE IS NOT EVIDENCE ABOUT THE BACKUP. A verification host that is down must never
  // report a well-protected database as unrecoverable.
  [Fact]
  public void A_verification_infrastructure_failure_never_produces_unprotected()
  {
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
      TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable, Healthy(), Now);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, status);
  }

  [Fact]
  public void A_verification_infrastructure_failure_preserves_held_degraded_when_visible_evidence_looks_protected()
  {
    var inputs = Healthy() with
    {
      HeldRecoveryReadinessStatus = TenantDatabaseRecoveryReadinessStatus.Degraded
    };

    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
      TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable, inputs, Now);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, status);
  }

  // ...but it does not conceal a verification that has since aged out either: readiness is recomputed from
  // the evidence already held.
  [Fact]
  public void A_verification_infrastructure_failure_still_reports_overdue_when_evidence_has_aged_out()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = 30,
      LastRestoreVerificationUtc = Now.AddDays(-45)
    };

    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
      TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable, inputs, Now);

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, status);
  }

  // ---- The partial post-backup observation.

  // Phase B reported VerificationOverdue unconditionally. A policy that never asked for periodic restore
  // verification must no longer be told it is overdue for one.
  [Fact]
  public void A_successful_backup_does_not_report_overdue_when_no_verification_is_required()
  {
    var inputs = Healthy() with { RestoreVerificationIntervalDays = null };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Degraded,
      TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterSuccessfulBackup(inputs, Now));
  }

  [Fact]
  public void A_successful_backup_reports_overdue_when_verification_is_required_and_missing()
  {
    var inputs = Healthy() with
    {
      RestoreVerificationIntervalDays = 30,
      LastRestoreVerificationUtc = null
    };

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.VerificationOverdue,
      TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterSuccessfulBackup(inputs, Now));
  }

  // The post-backup path has observed neither the recovery model nor chain continuity, so it must not claim
  // Protected however healthy the timestamps look.
  [Fact]
  public void A_successful_backup_never_reports_protected()
  {
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterSuccessfulBackup(Healthy(), Now);

    Assert.NotEqual(TenantDatabaseRecoveryReadinessStatus.Protected, status);
  }

  // ---- Depth selection (ADR-022 §17, v1.2).

  [Fact]
  public void A_full_only_policy_requires_level_a()
  {
    var inputs = Healthy() with
    {
      DifferentialBackupIntervalMinutes = null,
      TransactionLogBackupIntervalMinutes = null
    };

    Assert.Equal(
      TenantDatabaseRestoreDepth.Full,
      TenantDatabaseRecoveryReadinessEvaluator.RequiredDepth(inputs));
  }

  [Fact]
  public void A_differential_policy_requires_level_b()
  {
    var inputs = Healthy() with
    {
      DifferentialBackupIntervalMinutes = 1_440,
      TransactionLogBackupIntervalMinutes = null
    };

    Assert.Equal(
      TenantDatabaseRestoreDepth.FullWithDifferential,
      TenantDatabaseRecoveryReadinessEvaluator.RequiredDepth(inputs));
  }

  [Fact]
  public void A_log_policy_requires_level_c()
  {
    var inputs = Healthy() with { TransactionLogBackupIntervalMinutes = 15 };

    Assert.Equal(
      TenantDatabaseRestoreDepth.FullWithDifferentialAndLog,
      TenantDatabaseRecoveryReadinessEvaluator.RequiredDepth(inputs));
  }

  // A differential is not a precondition for log verification: a log policy with no differential schedule
  // is still Level C.
  [Fact]
  public void A_log_policy_without_a_differential_schedule_is_still_level_c()
  {
    var inputs = Healthy() with
    {
      DifferentialBackupIntervalMinutes = null,
      TransactionLogBackupIntervalMinutes = 15
    };

    Assert.Equal(
      TenantDatabaseRestoreDepth.FullWithDifferentialAndLog,
      TenantDatabaseRecoveryReadinessEvaluator.RequiredDepth(inputs));
  }

  private static TenantDatabaseRecoveryReadinessInputs Healthy() =>
    new(
      TenantDatabaseHostingMode.PlatformManaged,
      PolicyExists: true,
      PolicyEnabled: true,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      FullBackupIntervalMinutes: 10_080,
      DifferentialBackupIntervalMinutes: 1_440,
      TransactionLogBackupIntervalMinutes: null,
      RestoreVerificationIntervalDays: 30,
      MaximumBackupAgeMinutes: null,
      LastSuccessfulFullBackupUtc: Now.AddDays(-1),
      LastSuccessfulDifferentialBackupUtc: Now.AddHours(-2),
      LastSuccessfulLogBackupUtc: null,
      LastRestoreVerificationUtc: Now.AddDays(-2),
      ObservedRecoveryModel: TenantDatabaseRecoveryModel.Full);
}
