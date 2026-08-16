using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// The restore-verification lifecycle (ADR-022 §17, TS-Backup Phase D).
//
// The property worth most attention here is that a CLEANUP FAILURE CANNOT REACH THE VERIFICATION RESULT.
// That separation is what lets the platform report an orphan without discarding proof that the chain
// restored, and the way it is guaranteed is that the aggregate offers no path to express it.
[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseRestoreVerificationRunTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

  private const string Actor = "test";

  [Fact]
  public void An_admitted_run_starts_active_with_nothing_to_clean_up()
  {
    var run = Admit();

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Admitted, run.Status);
    Assert.Equal(TenantDatabaseVerificationCleanupState.NotRequired, run.CleanupState);
    Assert.Null(run.VerificationDatabaseName);
    Assert.True(run.IsActive);
  }

  [Fact]
  public void A_run_requires_a_baseline_to_restore()
  {
    var result = TenantDatabaseRestoreVerificationRun.Admit(
      42, 0, TenantDatabaseRestoreDepth.Full, "verify", Actor, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationBaselineRequired.Code, result.Error.Code);
  }

  [Fact]
  public void A_run_requires_a_restore_server_key()
  {
    var result = TenantDatabaseRestoreVerificationRun.Admit(
      42, 9, TenantDatabaseRestoreDepth.Full, "   ", Actor, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationServerKeyInvalid.Code, result.Error.Code);
  }

  // THE CRASH-SURVIVABILITY ORDER: the database is named on the record before it exists, never after.
  [Fact]
  public void Beginning_a_restore_records_the_database_name_and_marks_cleanup_pending()
  {
    var run = Admit();

    var result = run.BeginRestore("SSAS_Verify_42_7", Actor, Now);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Restoring, run.Status);
    Assert.Equal("SSAS_Verify_42_7", run.VerificationDatabaseName);
    Assert.Equal(TenantDatabaseVerificationCleanupState.Pending, run.CleanupState);
    Assert.True(run.IsActive);
  }

  [Fact]
  public void A_restore_cannot_begin_twice()
  {
    var run = Admit();
    run.BeginRestore("SSAS_Verify_42_7", Actor, Now);

    var result = run.BeginRestore("SSAS_Verify_42_8", Actor, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationNotAdmitted.Code, result.Error.Code);
    Assert.Equal("SSAS_Verify_42_7", run.VerificationDatabaseName);
  }

  [Fact]
  public void A_run_cannot_succeed_without_having_started_a_restore()
  {
    var run = Admit();

    var result = run.Succeed(Actor, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationNotRunning.Code, result.Error.Code);
  }

  [Fact]
  public void A_succeeded_run_is_terminal_and_no_longer_active()
  {
    var run = Restoring();

    Assert.True(run.Succeed(Actor, Now.AddMinutes(5)).IsSuccess);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, run.Status);
    Assert.Equal(Now.AddMinutes(5), run.CompletedUtc);
    Assert.False(run.IsActive);
    Assert.True(run.Fail("late", Actor, Now.AddMinutes(6)).IsFailure);
  }

  // THE SEPARATION THAT MATTERS. A failed drop after a proven restore must leave the verification result
  // untouched, or a durability signal would be driven by an operational one.
  [Fact]
  public void A_cleanup_failure_never_disturbs_a_succeeded_verification()
  {
    var run = Restoring();
    run.Succeed(Actor, Now.AddMinutes(5));

    var result = run.RecordCleanup(
      TenantDatabaseVerificationCleanupState.Failed, "drop blocked", Actor, Now.AddMinutes(6));

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Succeeded, run.Status);
    Assert.Equal(TenantDatabaseVerificationCleanupState.Failed, run.CleanupState);
    Assert.Contains("drop blocked", run.ErrorSummary, StringComparison.Ordinal);
  }

  [Fact]
  public void A_successful_cleanup_resolves_the_pending_state()
  {
    var run = Restoring();
    run.Succeed(Actor, Now.AddMinutes(5));

    run.RecordCleanup(TenantDatabaseVerificationCleanupState.Succeeded, null, Actor, Now.AddMinutes(6));

    Assert.Equal(TenantDatabaseVerificationCleanupState.Succeeded, run.CleanupState);
    Assert.Null(run.ErrorSummary);
  }

  // Cleanup is a TERMINAL outcome. Rewinding it to pending or not-required would erase the fact that a
  // database was created.
  [Theory]
  [InlineData(TenantDatabaseVerificationCleanupState.NotRequired)]
  [InlineData(TenantDatabaseVerificationCleanupState.Pending)]
  public void A_non_terminal_cleanup_outcome_is_refused(TenantDatabaseVerificationCleanupState state)
  {
    var run = Restoring();

    var result = run.RecordCleanup(state, null, Actor, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationCleanupStateInvalid.Code, result.Error.Code);
  }

  // INFRASTRUCTURE UNAVAILABILITY IS ITS OWN TERMINAL STATE, not a flavour of failure — a verification host
  // that is down says nothing about whether the backups would restore.
  [Fact]
  public void An_unavailable_verification_host_is_recorded_distinctly_from_failure()
  {
    var run = Admit();

    var result = run.AbandonUnavailable("verification server unreachable", Actor, Now.AddMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, run.Status);
    Assert.NotEqual(TenantDatabaseRestoreVerificationStatus.Failed, run.Status);
    Assert.False(run.IsActive);
  }

  [Fact]
  public void An_error_summary_is_bounded()
  {
    var run = Restoring();

    run.Fail(new string('x', 4_000), Actor, Now);

    Assert.NotNull(run.ErrorSummary);
    Assert.True(run.ErrorSummary!.Length <= TenantDatabaseRestoreVerificationRun.ErrorSummaryMaximumLength);
  }

  private static TenantDatabaseRestoreVerificationRun Admit()
  {
    var result = TenantDatabaseRestoreVerificationRun.Admit(
      42, 9, TenantDatabaseRestoreDepth.FullWithDifferential, "verify", Actor, Now);
    Assert.True(result.IsSuccess);
    return result.Value;
  }

  private static TenantDatabaseRestoreVerificationRun Restoring()
  {
    var run = Admit();
    run.BeginRestore("SSAS_Verify_42_7", Actor, Now);
    return run;
  }
}
