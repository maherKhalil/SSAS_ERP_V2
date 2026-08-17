using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Assembles the durable evidence a Dedicated activation decision reads (TS-Storage Phase E).
//
// THREE READS, NOT ONE JOIN. The registry row, the current baseline and the last successful verification are
// each a distinct "latest" question, and expressing them as one projection would either need correlated
// subqueries EF translates poorly or an outer join that multiplies rows. They are read consecutively on the
// same context, and the gate is conservative about disagreement between them rather than assuming a single
// consistent snapshot: a `Protected` row whose baseline has since moved is refused, not reconciled.
//
// EVERYTHING IS KEYED ON THE PHYSICAL DATABASE. A shared database has one chain and one verification
// history no matter how many tenants are assigned to it.
public sealed class TenantDatabaseRecoveryActivationReadRepository(PlatformDbContext dbContext)
  : ITenantDatabaseRecoveryActivationReadRepository
{
  public async Task<TenantDatabaseRecoveryActivationEvidence?> FindActivationEvidenceAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default)
  {
    if (tenantDatabaseId <= 0)
    {
      return null;
    }

    var database = await dbContext.Set<TenantDatabase>()
      .AsNoTracking()
      .Where(candidate => candidate.Id == tenantDatabaseId)
      .Select(candidate => new
      {
        candidate.Id,
        candidate.HostingMode,
        candidate.StorageMode,
        candidate.ProvisioningStatus,
        candidate.RecoveryReadinessStatus,
        candidate.LastSuccessfulFullBackupUtc,
        candidate.LastSuccessfulDifferentialBackupUtc,
        candidate.LastSuccessfulLogBackupUtc,
        candidate.LastRestoreVerificationUtc
      })
      .FirstOrDefaultAsync(cancellationToken);

    if (database is null)
    {
      return null;
    }

    // SingleOrDefault, matching the backup read repository: a unique index makes two policies for one
    // physical database structurally impossible, so a second one should throw rather than be picked from.
    var policy = await dbContext.TenantDatabaseBackupPolicies
      .AsNoTracking()
      .Where(candidate => candidate.TenantDatabaseId == tenantDatabaseId)
      .Select(candidate => new
      {
        candidate.Enabled,
        candidate.ManagementMode,
        candidate.FullBackupIntervalMinutes,
        candidate.DifferentialBackupIntervalMinutes,
        candidate.TransactionLogBackupIntervalMinutes,
        candidate.RestoreVerificationIntervalDays,
        candidate.MaximumBackupAgeMinutes
      })
      .SingleOrDefaultAsync(cancellationToken);

    // THE CURRENT BASELINE — the full backup a restore would start from right now. Ordered exactly as the
    // admission path orders it, so the gate and the verifier agree on which run is "the" baseline.
    var currentBaselineBackupRunId = await dbContext.TenantDatabaseBackupRuns
      .AsNoTracking()
      .Where(run => run.TenantDatabaseId == tenantDatabaseId &&
        run.Status == TenantDatabaseBackupRunStatus.Succeeded &&
        run.Operation.OperationCode == "Full")
      .OrderByDescending(run => run.Id)
      .Select(run => (long?)run.Id)
      .FirstOrDefaultAsync(cancellationToken);

    // THE EXACT VERIFICATION. The latest SUCCEEDED run, carrying the baseline it exercised and the depth it
    // reached — deliberately not filtered to the current baseline, so the gate can distinguish "never
    // verified" from "verified against a chain that has since moved on" and report the right one.
    var verification = await dbContext.TenantDatabaseRestoreVerificationRuns
      .AsNoTracking()
      .Where(run => run.TenantDatabaseId == tenantDatabaseId &&
        run.Status == TenantDatabaseRestoreVerificationStatus.Succeeded)
      .OrderByDescending(run => run.Id)
      .Select(run => new
      {
        VerificationRunId = (long?)run.Id,
        SourceBackupRunId = (long?)run.SourceBackupRunId,
        Depth = (TenantDatabaseRestoreDepth?)run.Depth,
        run.CompletedUtc
      })
      .FirstOrDefaultAsync(cancellationToken);

    return new TenantDatabaseRecoveryActivationEvidence(
      database.Id,
      database.HostingMode,
      database.StorageMode,
      database.ProvisioningStatus,
      PolicyExists: policy is not null,
      PolicyEnabled: policy?.Enabled ?? false,
      ManagementMode: policy?.ManagementMode ?? TenantDatabaseBackupManagementMode.CustomerDba,
      policy?.FullBackupIntervalMinutes,
      policy?.DifferentialBackupIntervalMinutes,
      policy?.TransactionLogBackupIntervalMinutes,
      policy?.RestoreVerificationIntervalDays,
      policy?.MaximumBackupAgeMinutes,
      database.RecoveryReadinessStatus,
      database.LastSuccessfulFullBackupUtc,
      database.LastSuccessfulDifferentialBackupUtc,
      database.LastSuccessfulLogBackupUtc,
      database.LastRestoreVerificationUtc,
      currentBaselineBackupRunId,
      verification?.VerificationRunId,
      verification?.SourceBackupRunId,
      verification?.Depth,
      verification?.CompletedUtc);
  }
}
