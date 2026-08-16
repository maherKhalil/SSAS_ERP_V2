using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Owns time-driven recovery-readiness recomputation. Schedulers may request a refresh, but this component
// alone gathers the complete policy/evidence model and writes the recovery dimension through its dedicated
// writer.
public interface ITenantDatabaseRecoveryReadinessRefresher
{
  Task RefreshAsync(long tenantDatabaseId, CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseRecoveryReadinessRefresher(
  ITenantDatabaseRegistryReadRepository registry,
  ITenantDatabaseBackupReadRepository backupReads,
  ITenantDatabaseRestoreVerificationFleetReadRepository verificationReads,
  ITenantDatabaseRecoveryReadinessWriter recoveryWriter,
  IDateTimeProvider clock) : ITenantDatabaseRecoveryReadinessRefresher
{
  private const string Actor = "tenant-recovery-readiness-refresher";

  public async Task RefreshAsync(long tenantDatabaseId, CancellationToken cancellationToken = default)
  {
    var page = await registry.ListPhysicalDatabasesAsync(tenantDatabaseId - 1, 1, cancellationToken);
    var database = page.Count == 1 && page[0].TenantDatabaseId == tenantDatabaseId ? page[0] : null;
    if (database is null)
    {
      return;
    }

    var policy = await backupReads.FindPolicyAsync(tenantDatabaseId, cancellationToken);
    var evidence = await backupReads.FindRecoveryEvidenceAsync(tenantDatabaseId, cancellationToken);
    var durableEvidence = await verificationReads.FindDurableRecoveryEvidenceAsync(
      tenantDatabaseId, cancellationToken);
    var inputs = Inputs(database, policy, evidence, durableEvidence);

    // This is an evidence refresh, not a new recovery-model observation. Reuse the evaluator's
    // uncertainty path so a held D7 Degraded outcome cannot become Protected merely from fresh timestamps.
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
      TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable, inputs, clock.UtcNow);
    if (status is null)
    {
      return;
    }

    await recoveryWriter.RecordRecoveryReadinessAsync(
      tenantDatabaseId,
      status.Value,
      Actor,
      durableEvidence?.LastSuccessfulFullBackupUtc,
      durableEvidence?.LastSuccessfulDifferentialBackupUtc,
      durableEvidence?.LastSuccessfulLogBackupUtc,
      durableEvidence?.LastSuccessfulRestoreVerificationUtc,
      cancellationToken);
  }

  private static TenantDatabaseRecoveryReadinessInputs Inputs(
    TenantDatabaseDescriptor database,
    TenantDatabaseBackupPolicyRecord? policy,
    TenantDatabaseRecoveryEvidenceRecord? evidence,
    TenantDatabaseDurableRecoveryEvidence? durableEvidence) =>
    new(
      database.HostingMode,
      PolicyExists: policy is not null,
      PolicyEnabled: policy?.Enabled ?? false,
      ManagementMode: policy?.ManagementMode ?? TenantDatabaseBackupManagementMode.CustomerDba,
      FullBackupIntervalMinutes: policy?.FullBackupIntervalMinutes,
      DifferentialBackupIntervalMinutes: policy?.DifferentialBackupIntervalMinutes,
      TransactionLogBackupIntervalMinutes: policy?.TransactionLogBackupIntervalMinutes,
      RestoreVerificationIntervalDays: policy?.RestoreVerificationIntervalDays,
      MaximumBackupAgeMinutes: policy?.MaximumBackupAgeMinutes,
      durableEvidence?.LastSuccessfulFullBackupUtc,
      durableEvidence?.LastSuccessfulDifferentialBackupUtc,
      durableEvidence?.LastSuccessfulLogBackupUtc,
      durableEvidence?.LastSuccessfulRestoreVerificationUtc,
      evidence?.RecoveryReadinessStatus == TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid
        ? TenantDatabaseRecoveryModel.Simple
        : null,
      PlatformChainBreakDetected:
        evidence?.RecoveryReadinessStatus == TenantDatabaseRecoveryReadinessStatus.Unprotected,
      HeldRecoveryReadinessStatus:
        evidence?.RecoveryReadinessStatus ?? TenantDatabaseRecoveryReadinessStatus.Unknown);
}
