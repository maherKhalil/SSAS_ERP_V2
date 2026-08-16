using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// D9's read-only physical database projection. It never materialises aggregates and it reads successful
// verification evidence from the durable operation row, not the lagging aggregate timestamp.
public sealed class TenantDatabaseRestoreVerificationFleetReadRepository(PlatformDbContext dbContext)
  : ITenantDatabaseRestoreVerificationFleetReadRepository
{
  public async Task<IReadOnlyList<string>> ListEligibleSourceServerKeysAsync(
    CancellationToken cancellationToken = default) =>
    await (from database in dbContext.TenantDatabases.AsNoTracking()
           join policy in dbContext.TenantDatabaseBackupPolicies.AsNoTracking()
             on database.Id equals policy.TenantDatabaseId
           where database.HostingMode == TenantDatabaseHostingMode.PlatformManaged &&
             database.ProvisioningStatus == TenantDatabaseProvisioningStatus.Ready &&
             policy.Enabled &&
             policy.ManagementMode == TenantDatabaseBackupManagementMode.AutomaticByPlatform &&
             policy.RestoreVerificationIntervalDays != null
           select database.ServerKey)
      .Distinct()
      .OrderBy(serverKey => serverKey)
      .ToListAsync(cancellationToken);

  public async Task<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>> ListCandidatesAsync(
    string sourceServerKey,
    long afterTenantDatabaseId,
    int take,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sourceServerKey);
    if (afterTenantDatabaseId < 0 || take <= 0)
    {
      throw new ArgumentOutOfRangeException(take <= 0 ? nameof(take) : nameof(afterTenantDatabaseId));
    }

    return await (from database in dbContext.TenantDatabases.AsNoTracking()
                  join policy in dbContext.TenantDatabaseBackupPolicies.AsNoTracking()
                    on database.Id equals policy.TenantDatabaseId
                  where database.ServerKey == sourceServerKey &&
                    database.Id > afterTenantDatabaseId &&
                    database.HostingMode == TenantDatabaseHostingMode.PlatformManaged &&
                    database.ProvisioningStatus == TenantDatabaseProvisioningStatus.Ready &&
                    policy.Enabled &&
                    policy.ManagementMode == TenantDatabaseBackupManagementMode.AutomaticByPlatform &&
                    policy.RestoreVerificationIntervalDays != null
                  orderby database.Id
                  select new TenantDatabaseRestoreVerificationDueCandidate(
                    database.Id,
                    database.ServerKey,
                    database.HostingMode,
                    database.ProvisioningStatus,
                    policy.ManagementMode,
                    policy.Enabled,
                    policy.DifferentialBackupIntervalMinutes,
                    policy.TransactionLogBackupIntervalMinutes,
                    policy.RestoreVerificationIntervalDays,
                    dbContext.TenantDatabaseBackupRuns
                      .Where(run => run.TenantDatabaseId == database.Id &&
                        run.Status == TenantDatabaseBackupRunStatus.Succeeded &&
                        run.Operation.ProviderKey == "SqlServer" && run.Operation.OperationCode == "Full")
                      .OrderByDescending(run => run.Id)
                      .Select(run => (long?)run.Id)
                      .FirstOrDefault(),
                    dbContext.TenantDatabaseRestoreVerificationRuns
                      .Where(run => run.TenantDatabaseId == database.Id &&
                        run.Status == TenantDatabaseRestoreVerificationStatus.Succeeded)
                      .OrderByDescending(run => run.CompletedUtc)
                      .ThenByDescending(run => run.Id)
                      .Select(run => (long?)run.Id)
                      .FirstOrDefault(),
                    dbContext.TenantDatabaseRestoreVerificationRuns
                      .Where(run => run.TenantDatabaseId == database.Id &&
                        run.Status == TenantDatabaseRestoreVerificationStatus.Succeeded)
                      .OrderByDescending(run => run.CompletedUtc)
                      .ThenByDescending(run => run.Id)
                      .Select(run => run.CompletedUtc)
                      .FirstOrDefault()))
      .Take(take)
      .ToListAsync(cancellationToken);
  }

  public Task<TenantDatabaseDurableRecoveryEvidence?> FindDurableRecoveryEvidenceAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantDatabases
      .AsNoTracking()
      .Where(database => database.Id == tenantDatabaseId)
      .Select(database => new TenantDatabaseDurableRecoveryEvidence(
        database.Id,
        dbContext.TenantDatabaseBackupRuns
          .Where(run => run.TenantDatabaseId == database.Id &&
            run.Status == TenantDatabaseBackupRunStatus.Succeeded &&
            run.Operation.ProviderKey == "SqlServer" && run.Operation.OperationCode == "Full")
          .OrderByDescending(run => run.CompletedUtc)
          .ThenByDescending(run => run.Id)
          .Select(run => run.CompletedUtc)
          .FirstOrDefault(),
        dbContext.TenantDatabaseBackupRuns
          .Where(run => run.TenantDatabaseId == database.Id &&
            run.Status == TenantDatabaseBackupRunStatus.Succeeded &&
            run.Operation.ProviderKey == "SqlServer" && run.Operation.OperationCode == "Differential")
          .OrderByDescending(run => run.CompletedUtc)
          .ThenByDescending(run => run.Id)
          .Select(run => run.CompletedUtc)
          .FirstOrDefault(),
        dbContext.TenantDatabaseBackupRuns
          .Where(run => run.TenantDatabaseId == database.Id &&
            run.Status == TenantDatabaseBackupRunStatus.Succeeded &&
            run.Operation.ProviderKey == "SqlServer" && run.Operation.OperationCode == "TransactionLog")
          .OrderByDescending(run => run.CompletedUtc)
          .ThenByDescending(run => run.Id)
          .Select(run => run.CompletedUtc)
          .FirstOrDefault(),
        dbContext.TenantDatabaseRestoreVerificationRuns
          .Where(run => run.TenantDatabaseId == database.Id &&
            run.Status == TenantDatabaseRestoreVerificationStatus.Succeeded)
          .OrderByDescending(run => run.CompletedUtc)
          .ThenByDescending(run => run.Id)
          .Select(run => run.CompletedUtc)
          .FirstOrDefault()))
      .SingleOrDefaultAsync(cancellationToken);
}
