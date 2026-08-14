using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// Backup run history invariants (ADR-022 §15). Phase A models the record; nothing here executes a backup.
public sealed class TenantDatabaseBackupRunTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_run_starts_against_a_physical_database_with_a_provider_scoped_operation()
  {
    var run = Start().Value;

    Assert.Equal(TenantDatabaseBackupRunStatus.Running, run.Status);
    Assert.Equal("SqlServer", run.Operation.ProviderKey);
    Assert.Equal("Full", run.Operation.OperationCode);
    Assert.Equal("PrimaryBackupVault", run.DestinationKey);
    Assert.Null(run.CompletedUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_operations_are_provider_scoped_rather_than_a_universal_vocabulary()
  {
    // ADR-022 §10: Full/Differential/TransactionLog are SQL Server vocabulary. A future provider registers
    // its own codes rather than being remapped onto these three.
    Assert.Equal("SqlServer", TenantDatabaseBackupOperation.SqlServerDifferential().ProviderKey);
    Assert.Equal("TransactionLog", TenantDatabaseBackupOperation.SqlServerTransactionLog().OperationCode);

    var other = TenantDatabaseBackupOperation.Create("Oracle", "ArchivedRedo");
    Assert.True(other.IsSuccess);
    Assert.NotEqual(TenantDatabaseBackupOperation.SqlServerFull(), other.Value);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Success_requires_provider_evidence_not_a_completed_command()
  {
    // The central rule of ADR-022 §14: an ExecuteNonQuery that returned is not evidence that a usable
    // backup exists, so a run cannot become Succeeded without reconciled provider identity.
    var run = Start().Value;

    var withoutEvidence = run.Succeed(
      "   ", null, null, null, null, null, null, "actor", Now.AddMinutes(5));

    Assert.True(withoutEvidence.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupProviderEvidenceRequired.Code, withoutEvidence.Error.Code);
    Assert.Equal(TenantDatabaseBackupRunStatus.Running, run.Status);

    var withEvidence = run.Succeed(
      "backup-set-4711", "vault/full/4711", 2_048, 100m, 200m, 100m, Guid.NewGuid(), "actor", Now.AddMinutes(5));

    Assert.True(withEvidence.IsSuccess);
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, run.Status);
    Assert.Equal("backup-set-4711", run.ProviderBackupIdentity);
    Assert.Equal(Now.AddMinutes(5), run.CompletedUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Chain_metadata_is_recorded_from_the_first_backup()
  {
    // Reconstructing chain continuity retrospectively is far harder than capturing it, so the LSN triple
    // and backup-set identity are recorded at success time (ADR-022 §9).
    var run = Start().Value;
    var setGuid = Guid.NewGuid();

    run.Succeed("set-1", null, null, 10m, 20m, 5m, setGuid, "actor", Now.AddMinutes(1));

    Assert.Equal(10m, run.FirstLsn);
    Assert.Equal(20m, run.LastLsn);
    Assert.Equal(5m, run.DatabaseBackupLsn);
    Assert.Equal(setGuid, run.BackupSetGuid);
  }

  [Theory]
  [InlineData(TenantDatabaseBackupRunStatus.SkippedOwnershipHeld)]
  [InlineData(TenantDatabaseBackupRunStatus.SkippedInFlightOperation)]
  [InlineData(TenantDatabaseBackupRunStatus.BlockedByPolicy)]
  [Trait("Decision", "ADR-022")]
  public void A_controlled_skip_is_not_a_failure(TenantDatabaseBackupRunStatus status)
  {
    // Nothing failed: another worker holds ownership, a server-side backup is still in flight, or policy
    // forbids execution. Recording these as failures would degrade recovery readiness on the strength of
    // coordination working exactly as designed (ADR-022 §14).
    var run = Start().Value;

    var result = run.Skip(status, "another worker holds ownership", "actor", Now.AddMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.Equal(status, run.Status);
    Assert.NotEqual(TenantDatabaseBackupRunStatus.Failed, run.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Skip_refuses_a_status_that_is_not_a_controlled_outcome()
  {
    var run = Start().Value;

    var result = run.Skip(TenantDatabaseBackupRunStatus.Succeeded, null, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupRunSkipStatusInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_error_summary_is_bounded_so_a_provider_exception_cannot_blow_up_the_row()
  {
    var run = Start().Value;

    run.Fail(new string('x', TenantDatabaseBackupRun.ErrorSummaryMaximumLength * 3), "actor", Now.AddMinutes(1));

    Assert.Equal(TenantDatabaseBackupRunStatus.Failed, run.Status);
    Assert.NotNull(run.ErrorSummary);
    Assert.Equal(TenantDatabaseBackupRun.ErrorSummaryMaximumLength, run.ErrorSummary!.Length);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Readability_verification_is_recorded_distinctly_from_a_real_restore()
  {
    // RESTORE VERIFYONLY proves the set is readable, not that the database is recoverable (ADR-022 §17), so
    // the two levels are different values rather than one verified flag.
    var run = Start().Value;
    run.Succeed("set-1", null, null, null, null, null, null, "actor", Now.AddMinutes(1));

    run.RecordVerification(
      TenantDatabaseBackupVerificationState.ReadabilityVerified, null, "actor", Now.AddMinutes(2));
    Assert.Equal(TenantDatabaseBackupVerificationState.ReadabilityVerified, run.VerificationState);
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, run.Status);

    run.RecordVerification(
      TenantDatabaseBackupVerificationState.RestoreVerified, null, "actor", Now.AddMinutes(3));
    Assert.Equal(TenantDatabaseBackupVerificationState.RestoreVerified, run.VerificationState);
    Assert.Equal(Now.AddMinutes(3), run.LastVerifiedUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Recording_not_verified_would_erase_evidence_and_is_refused()
  {
    var run = Start().Value;

    var result = run.RecordVerification(
      TenantDatabaseBackupVerificationState.NotVerified, null, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupVerificationResultRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_negative_size_is_rejected()
  {
    var run = Start().Value;

    var result = run.Succeed("set-1", null, -1, null, null, null, null, "actor", Now.AddMinutes(1));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupRunSizeInvalid.Code, result.Error.Code);
  }

  private static SSAS.BuildingBlocks.Domain.Result<TenantDatabaseBackupRun> Start() =>
    TenantDatabaseBackupRun.Start(
      1, TenantDatabaseBackupOperation.SqlServerFull(), "PrimaryBackupVault", "actor", Now);
}
