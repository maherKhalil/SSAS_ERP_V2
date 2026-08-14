using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// The FOURTH dimension on the aggregate (ADR-022 §2, §3). These prove it is genuinely independent: writing
// it leaves the ADR-018 dimensions exactly as they were, and it cannot be talked into claiming protection.
public sealed class TenantDatabaseRecoveryReadinessTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_newly_registered_database_has_unknown_recovery_readiness()
  {
    // Nothing has proven this database recoverable, and the lifecycle gates that read this must fail closed
    // on that rather than inherit a comfortable assumption.
    var database = Register();

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unknown, database.RecoveryReadinessStatus);
    Assert.Null(database.LastRecoveryReadinessCheckUtc);
    Assert.Null(database.LastSuccessfulFullBackupUtc);
    Assert.Null(database.LastRestoreVerificationUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Recording_readiness_writes_only_the_recovery_dimension()
  {
    // The property the whole one-writer-per-dimension discipline exists to protect, at aggregate level.
    var database = Register();
    database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, "connectivity", Now);
    database.RecordSchemaHealth(
      TenantDatabaseSchemaCompatibilityStatus.UpToDate, "20260814_A", "20260814_A", "schema", Now);

    database.RecordRecoveryReadiness(
      TenantDatabaseRecoveryReadinessStatus.Degraded, "recovery", Now.AddMinutes(5),
      lastSuccessfulFullBackupUtc: Now.AddDays(-2));

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, database.RecoveryReadinessStatus);
    Assert.Equal(Now.AddMinutes(5), database.LastRecoveryReadinessCheckUtc);

    // Untouched, all of it.
    Assert.Equal(TenantDatabaseConnectivityStatus.Healthy, database.ConnectivityStatus);
    Assert.Equal(Now, database.LastConnectivityCheckUtc);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.UpToDate, database.SchemaCompatibilityStatus);
    Assert.Equal("20260814_A", database.AppliedMigration);
    Assert.Equal(Now, database.LastSchemaCheckUtc);
    Assert.Equal(TenantDatabaseMigrationExecutionStatus.Idle, database.MigrationExecutionStatus);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_evaluation_that_observed_nothing_about_a_backup_type_leaves_that_timestamp_alone()
  {
    // "A check that observes nothing writes nothing", applied within the dimension: a readiness evaluation
    // that learned nothing new about the log chain must not erase what the last one recorded.
    var database = Register();
    database.RecordRecoveryReadiness(
      TenantDatabaseRecoveryReadinessStatus.Protected, "recovery", Now,
      lastSuccessfulFullBackupUtc: Now.AddDays(-1),
      lastSuccessfulLogBackupUtc: Now.AddMinutes(-10));

    database.RecordRecoveryReadiness(
      TenantDatabaseRecoveryReadinessStatus.Degraded, "recovery", Now.AddHours(1));

    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Degraded, database.RecoveryReadinessStatus);
    Assert.Equal(Now.AddDays(-1), database.LastSuccessfulFullBackupUtc);
    Assert.Equal(Now.AddMinutes(-10), database.LastSuccessfulLogBackupUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Recording_unknown_would_erase_evidence_and_is_refused()
  {
    // Unknown is the pre-verification state, not a verdict — the same rule connectivity follows.
    var database = Register();

    var result = database.RecordRecoveryReadiness(
      TenantDatabaseRecoveryReadinessStatus.Unknown, "recovery", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RecoveryReadinessResultRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Recovery_readiness_is_a_distinct_status_type_from_the_other_dimensions()
  {
    // A guard against the merge this ADR exists to prevent: if recovery state were ever folded into schema
    // or connectivity vocabulary, this stops compiling.
    Assert.NotEqual(
      typeof(TenantDatabaseRecoveryReadinessStatus),
      typeof(TenantDatabaseSchemaCompatibilityStatus));
    Assert.Equal(
      6, Enum.GetValues<TenantDatabaseRecoveryReadinessStatus>().Length);
  }

  private static TenantDatabase Register() =>
    TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Dedicated,
      "PrimarySqlServer", "SSAS_Dedicated_01", TenantDatabaseProvisioningStatus.Ready, "actor", Now).Value;
}
