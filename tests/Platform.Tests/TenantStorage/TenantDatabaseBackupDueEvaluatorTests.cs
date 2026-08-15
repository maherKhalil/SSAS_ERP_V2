using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.TenantStorage;

// Due-time and precedence rules (ADR-022 §13, TS-Backup Phase C).
//
// Every one of these runs without a database. That is the point of keeping the evaluator pure: the rules
// that decide when a fleet gets backed up are the rules most worth exercising exhaustively, and they should
// not cost a SQL Server round trip to assert.
public sealed class TenantDatabaseBackupDueEvaluatorTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_full_is_due_when_none_has_ever_succeeded()
  {
    // The unprotected case: a policy exists, nothing has ever been backed up. This is the state a newly
    // provisioned dedicated database is in, and it is the one that must not be missed.
    var candidate = Candidate(fullInterval: 10_080);

    Assert.True(TenantDatabaseBackupDueEvaluator.IsFullDue(candidate, Now));
    Assert.Equal("Full", TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now)!.OperationCode);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_full_is_not_due_before_its_interval_elapses()
  {
    var candidate = Candidate(fullInterval: 60, lastFull: Now.AddMinutes(-59));

    Assert.False(TenantDatabaseBackupDueEvaluator.IsFullDue(candidate, Now));
    Assert.Null(TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_null_interval_means_not_scheduled_rather_than_overdue()
  {
    // The distinction that would otherwise turn an unconfigured cadence into a continuous backup loop: all
    // three interval columns are nullable, and "unset" is not "due since the beginning of time".
    var candidate = Candidate(fullInterval: null, differentialInterval: null, logInterval: null);

    Assert.False(TenantDatabaseBackupDueEvaluator.IsFullDue(candidate, Now));
    Assert.False(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(candidate, Now));
    Assert.False(TenantDatabaseBackupDueEvaluator.IsTransactionLogDue(candidate, Now));
    Assert.Null(TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_differential_requires_a_full_baseline()
  {
    // Nothing to be differential FROM. Scheduling one would produce a guaranteed provider block, so the full
    // becomes due instead.
    var candidate = Candidate(fullInterval: 10_080, differentialInterval: 1);

    Assert.False(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(candidate, Now));
    Assert.Equal("Full", TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now)!.OperationCode);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_differential_anchors_to_the_full_until_one_has_run()
  {
    // First differential falls due one interval after the full it depends on — not immediately after it.
    var recentFull = Candidate(differentialInterval: 60, lastFull: Now.AddMinutes(-59));
    Assert.False(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(recentFull, Now));

    var elapsedFull = Candidate(differentialInterval: 60, lastFull: Now.AddMinutes(-61));
    Assert.True(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(elapsedFull, Now));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_differential_anchors_to_the_last_differential_once_one_exists()
  {
    var candidate = Candidate(
      differentialInterval: 60,
      lastFull: Now.AddDays(-2),
      lastDifferential: Now.AddMinutes(-30));

    Assert.False(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(candidate, Now));
    Assert.True(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(candidate, Now.AddMinutes(31)));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_log_backup_requires_a_full_baseline()
  {
    // A database in FULL recovery with no full backup is pseudo-simple; its log chain does not exist yet.
    var candidate = Candidate(fullInterval: 10_080, logInterval: 15);

    Assert.False(TenantDatabaseBackupDueEvaluator.IsTransactionLogDue(candidate, Now));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_log_backup_anchors_to_the_full_then_to_the_last_log()
  {
    var fromFull = Candidate(logInterval: 15, lastFull: Now.AddMinutes(-16));
    Assert.True(TenantDatabaseBackupDueEvaluator.IsTransactionLogDue(fromFull, Now));

    var fromLog = Candidate(logInterval: 15, lastFull: Now.AddDays(-1), lastLog: Now.AddMinutes(-5));
    Assert.False(TenantDatabaseBackupDueEvaluator.IsTransactionLogDue(fromLog, Now));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Log_wins_when_every_operation_is_due()
  {
    // ADR-022 compliance rule 31. Log protects the recovery POINT, so it must not queue behind a long full.
    var candidate = Candidate(
      fullInterval: 60, differentialInterval: 60, logInterval: 15,
      lastFull: Now.AddDays(-7), lastDifferential: Now.AddDays(-7), lastLog: Now.AddDays(-7));

    Assert.True(TenantDatabaseBackupDueEvaluator.IsFullDue(candidate, Now));
    Assert.True(TenantDatabaseBackupDueEvaluator.IsDifferentialDue(candidate, Now));
    Assert.True(TenantDatabaseBackupDueEvaluator.IsTransactionLogDue(candidate, Now));

    Assert.Equal(
      "TransactionLog",
      TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now)!.OperationCode);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Full_wins_over_differential_when_the_log_is_not_due()
  {
    // A differential taken immediately before a due full is work the full makes redundant.
    var candidate = Candidate(
      fullInterval: 60, differentialInterval: 60, logInterval: null,
      lastFull: Now.AddDays(-7), lastDifferential: Now.AddDays(-7));

    Assert.Equal("Full", TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now)!.OperationCode);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Differential_is_selected_when_it_is_the_only_one_due()
  {
    var candidate = Candidate(
      fullInterval: 10_080, differentialInterval: 60, logInterval: null,
      lastFull: Now.AddMinutes(-120), lastDifferential: Now.AddMinutes(-61));

    Assert.Equal(
      "Differential",
      TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now)!.OperationCode);
  }

  [Theory]
  [Trait("Decision", "ADR-022")]
  [InlineData(false, TenantDatabaseBackupManagementMode.AutomaticByPlatform,
    TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseProvisioningStatus.Ready)]
  [InlineData(true, TenantDatabaseBackupManagementMode.CustomerDba,
    TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseProvisioningStatus.Ready)]
  [InlineData(true, TenantDatabaseBackupManagementMode.PlatformAfterApproval,
    TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseProvisioningStatus.Ready)]
  [InlineData(true, TenantDatabaseBackupManagementMode.AutomaticByPlatform,
    TenantDatabaseHostingMode.CustomerManaged, TenantDatabaseProvisioningStatus.Ready)]
  [InlineData(true, TenantDatabaseBackupManagementMode.AutomaticByPlatform,
    TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseProvisioningStatus.Provisioning)]
  public void Ineligible_databases_are_never_selected(
    bool enabled,
    TenantDatabaseBackupManagementMode managementMode,
    TenantDatabaseHostingMode hostingMode,
    TenantDatabaseProvisioningStatus provisioningStatus)
  {
    // Every one of these would otherwise be maximally "due" — no backup has ever run. The scheduler must
    // still refuse, and must not manufacture a BlockedByPolicy run on every sweep to say so.
    var candidate = Candidate(fullInterval: 60) with
    {
      PolicyEnabled = enabled,
      ManagementMode = managementMode,
      HostingMode = hostingMode,
      ProvisioningStatus = provisioningStatus
    };

    Assert.False(TenantDatabaseBackupDueEvaluator.IsEligible(candidate));
    Assert.Null(TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, Now));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_eligible_automatic_platform_managed_ready_database_is_selected()
  {
    Assert.True(TenantDatabaseBackupDueEvaluator.IsEligible(Candidate(fullInterval: 60)));
  }

  private static TenantDatabaseBackupDueCandidate Candidate(
    int? fullInterval = null,
    int? differentialInterval = null,
    int? logInterval = null,
    DateTimeOffset? lastFull = null,
    DateTimeOffset? lastDifferential = null,
    DateTimeOffset? lastLog = null) =>
    new(
      TenantDatabaseId: 1,
      ServerKey: "PrimarySqlServer",
      HostingMode: TenantDatabaseHostingMode.PlatformManaged,
      ProvisioningStatus: TenantDatabaseProvisioningStatus.Ready,
      ManagementMode: TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      PolicyEnabled: true,
      FullBackupIntervalMinutes: fullInterval,
      DifferentialBackupIntervalMinutes: differentialInterval,
      TransactionLogBackupIntervalMinutes: logInterval,
      LastSuccessfulFullBackupUtc: lastFull,
      LastSuccessfulDifferentialBackupUtc: lastDifferential,
      LastSuccessfulLogBackupUtc: lastLog);
}
