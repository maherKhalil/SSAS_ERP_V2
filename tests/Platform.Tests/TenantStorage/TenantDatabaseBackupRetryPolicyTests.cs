using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.TenantStorage;

// Retry suppression (ADR-022 §13, TS-Backup Phase C).
//
// These assert the separation that makes the scheduler honest: a failing database stays OVERDUE — readiness
// keeps telling the truth — while the scheduler simply declines to retry it every sweep.
public sealed class TenantDatabaseBackupRetryPolicyTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

  private static readonly TimeSpan Initial = TimeSpan.FromMinutes(5);

  private static readonly TimeSpan Maximum = TimeSpan.FromMinutes(60);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Nothing_ever_attempted_is_never_suppressed()
  {
    Assert.False(TenantDatabaseBackupRetryPolicy.ShouldSuppress(null, Now, Initial, Maximum));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_recent_failure_is_suppressed_and_an_old_one_is_retried()
  {
    var recent = Run(TenantDatabaseBackupRunStatus.Failed, Now.AddMinutes(-1));
    Assert.True(TenantDatabaseBackupRetryPolicy.ShouldSuppress(recent, Now, Initial, Maximum));

    var old = Run(TenantDatabaseBackupRunStatus.Failed, Now.AddMinutes(-6));
    Assert.False(TenantDatabaseBackupRetryPolicy.ShouldSuppress(old, Now, Initial, Maximum));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Skips_pause_only_briefly()
  {
    // A skip means coordination worked — another worker owns it, or an operation is genuinely running. That
    // is not a fault, so the pause is short rather than a failure backoff.
    foreach (var status in new[]
    {
      TenantDatabaseBackupRunStatus.SkippedOwnershipHeld,
      TenantDatabaseBackupRunStatus.SkippedInFlightOperation
    })
    {
      Assert.True(TenantDatabaseBackupRetryPolicy.ShouldSuppress(
        Run(status, Now.AddSeconds(-30)), Now, Initial, Maximum));

      Assert.False(TenantDatabaseBackupRetryPolicy.ShouldSuppress(
        Run(status, Now.AddMinutes(-2)), Now, Initial, Maximum));
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_successful_run_does_not_suppress_the_next_due_operation()
  {
    // Due-ness already accounts for success. Suppressing here as well would delay a log backup that has
    // genuinely fallen due since.
    Assert.False(TenantDatabaseBackupRetryPolicy.ShouldSuppress(
      Run(TenantDatabaseBackupRunStatus.Succeeded, Now.AddSeconds(-1)), Now, Initial, Maximum));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_blocked_policy_is_not_retried_on_a_timer()
  {
    // It clears when authority changes, not when a clock advances.
    Assert.True(TenantDatabaseBackupRetryPolicy.ShouldSuppress(
      Run(TenantDatabaseBackupRunStatus.BlockedByPolicy, Now.AddDays(-30)), Now, Initial, Maximum));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_running_operation_is_not_started_again()
  {
    Assert.True(TenantDatabaseBackupRetryPolicy.ShouldSuppress(
      Run(TenantDatabaseBackupRunStatus.Running, Now.AddSeconds(-5)), Now, Initial, Maximum));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backoff_escalates_and_is_capped()
  {
    Assert.Equal(Initial, TenantDatabaseBackupRetryPolicy.BackoffFor(1, Initial, Maximum));
    Assert.Equal(TimeSpan.FromMinutes(10), TenantDatabaseBackupRetryPolicy.BackoffFor(2, Initial, Maximum));
    Assert.Equal(TimeSpan.FromMinutes(20), TenantDatabaseBackupRetryPolicy.BackoffFor(3, Initial, Maximum));
    Assert.Equal(TimeSpan.FromMinutes(40), TenantDatabaseBackupRetryPolicy.BackoffFor(4, Initial, Maximum));

    // Capped, and a long failure streak must not overflow into a negative or absurd interval.
    Assert.Equal(Maximum, TenantDatabaseBackupRetryPolicy.BackoffFor(5, Initial, Maximum));
    Assert.Equal(Maximum, TenantDatabaseBackupRetryPolicy.BackoffFor(1_000, Initial, Maximum));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Suppression_measures_from_completion_where_one_exists()
  {
    // A backup that started hours ago and failed minutes ago is a recent failure, not an old one.
    var run = new TenantDatabaseBackupRunRecord(
      1, 1, "SqlServer", "Full", TenantDatabaseBackupRunStatus.Failed,
      StartedUtc: Now.AddHours(-3), CompletedUtc: Now.AddMinutes(-1),
      null, null, null, null, TenantDatabaseBackupVerificationState.NotVerified, null, "boom");

    Assert.True(TenantDatabaseBackupRetryPolicy.ShouldSuppress(run, Now, Initial, Maximum));
  }

  private static TenantDatabaseBackupRunRecord Run(TenantDatabaseBackupRunStatus status, DateTimeOffset when) =>
    new(1, 1, "SqlServer", "Full", status, when, when, null, null, null, null,
      TenantDatabaseBackupVerificationState.NotVerified, null, null);
}
