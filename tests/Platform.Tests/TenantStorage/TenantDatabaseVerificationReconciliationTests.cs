using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// Abandoned active-run reconciliation (ADR-022 §17, TS-Backup Phase D — LOW-C).
//
// The property worth most attention is what this REFUSES to do. Releasing an admission slot next to a live
// restore would permit a second restore of the same database alongside the first, so every path that cannot
// positively establish "nothing is running" resolves to leave the run alone.
[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseVerificationReconciliationTests
{
  // AGE ALONE IS NEVER SUFFICIENT. A legitimate restore of a large database can run for hours, and this is
  // the case that makes a timeout-only design wrong.
  [Fact]
  public void A_long_running_restore_is_left_alone_however_old_it_is()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Restoring,
      restoreIsActiveOnServer: true,
      age: TimeSpan.FromDays(2)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.LeaveAlone, decision);
  }

  // ABSENCE OF EVIDENCE IS NOT EVIDENCE OF ABSENCE. An unreachable verification host says nothing about
  // whether a restore is running on it.
  [Fact]
  public void A_run_is_left_alone_when_server_state_could_not_be_observed()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Restoring,
      serverStateObserved: false,
      age: TimeSpan.FromDays(2)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.LeaveAlone, decision);
  }

  [Fact]
  public void A_recent_run_is_left_alone_even_with_no_restore_running()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Restoring,
      age: TimeSpan.FromMinutes(1)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.LeaveAlone, decision);
  }

  // Admitted, never started, nothing running, past grace: the process died before creating anything.
  [Fact]
  public void An_admitted_run_that_never_started_may_be_released()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Admitted,
      verificationDatabaseExists: false,
      age: TimeSpan.FromHours(12)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.ReleaseAbandoned, decision);
  }

  // Restoring, nothing running, and a database left behind: abandoned, with an orphan to surface. This
  // slice records the orphan rather than dropping it, because the destructive-permission model is unproven.
  [Fact]
  public void An_abandoned_restore_that_left_a_database_reports_an_orphan()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Restoring,
      verificationDatabaseExists: true,
      age: TimeSpan.FromHours(12)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.ReleaseAbandonedWithOrphan, decision);
  }

  [Fact]
  public void An_abandoned_restore_that_left_nothing_is_simply_released()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Restoring,
      verificationDatabaseExists: false,
      age: TimeSpan.FromHours(12)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.ReleaseAbandoned, decision);
  }

  // The record says the run never started; the server says a database exists. No automated action should
  // resolve a disagreement between the two sources of truth.
  [Fact]
  public void A_record_and_server_disagreement_is_reported_rather_than_acted_on()
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: TenantDatabaseRestoreVerificationStatus.Admitted,
      verificationDatabaseExists: true,
      age: TimeSpan.FromHours(12)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.ReportInconsistent, decision);
  }

  // Terminal runs hold no admission slot, so they are never reconciliation candidates.
  [Theory]
  [InlineData(TenantDatabaseRestoreVerificationStatus.Succeeded)]
  [InlineData(TenantDatabaseRestoreVerificationStatus.Failed)]
  [InlineData(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable)]
  public void A_terminal_run_is_never_reconciled(TenantDatabaseRestoreVerificationStatus status)
  {
    var decision = TenantDatabaseVerificationReconciliation.Decide(Inputs(
      status: status,
      verificationDatabaseExists: true,
      age: TimeSpan.FromDays(30)));

    Assert.Equal(TenantDatabaseVerificationReconciliationDecision.LeaveAlone, decision);
  }

  private static TenantDatabaseVerificationReconciliationInputs Inputs(
    TenantDatabaseRestoreVerificationStatus status,
    bool serverStateObserved = true,
    bool restoreIsActiveOnServer = false,
    bool verificationDatabaseExists = false,
    TimeSpan? age = null) =>
    new(status, serverStateObserved, restoreIsActiveOnServer, verificationDatabaseExists,
      age ?? TimeSpan.FromHours(12), TimeSpan.FromHours(6));
}
