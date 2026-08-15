using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Fleet reads for backup scheduling (ADR-022 §13), projected flat so no EF entity escapes upward.
public sealed class TenantDatabaseBackupFleetReadRepository(PlatformDbContext dbContext)
  : ITenantDatabaseBackupFleetReadRepository
{
  public async Task<IReadOnlyList<string>> ListEligibleServerKeysAsync(
    CancellationToken cancellationToken = default) =>
    await (from database in dbContext.TenantDatabases.AsNoTracking()
           join policy in dbContext.TenantDatabaseBackupPolicies.AsNoTracking()
             on database.Id equals policy.TenantDatabaseId
           where policy.Enabled &&
             policy.ManagementMode == Domain.Enums.TenantDatabaseBackupManagementMode.AutomaticByPlatform &&
             database.HostingMode == Domain.Enums.TenantDatabaseHostingMode.PlatformManaged &&
             database.ProvisioningStatus == Domain.Enums.TenantDatabaseProvisioningStatus.Ready
           select database.ServerKey)
      .Distinct()
      .OrderBy(serverKey => serverKey)
      .ToListAsync(cancellationToken);

  public async Task<IReadOnlyList<TenantDatabaseBackupDueCandidate>> ListBackupCandidatesAsync(
    string serverKey,
    long afterId,
    int take,
    CancellationToken cancellationToken = default) =>
    // An INNER join to policy: a database with no backup policy is not a scheduling candidate at all, and
    // filtering it in the database keeps pages full of things worth evaluating rather than mostly nulls.
    //
    // Eligibility is applied HERE rather than in memory so a fleet of mostly customer-managed or disabled
    // databases does not page through the scheduler. It is an efficiency filter, not the security boundary —
    // the executor re-reads authority at execution and refuses independently.
    await (from database in dbContext.TenantDatabases.AsNoTracking()
           join policy in dbContext.TenantDatabaseBackupPolicies.AsNoTracking()
             on database.Id equals policy.TenantDatabaseId
           where database.ServerKey == serverKey &&
             database.Id > afterId &&
             policy.Enabled &&
             policy.ManagementMode == Domain.Enums.TenantDatabaseBackupManagementMode.AutomaticByPlatform &&
             database.HostingMode == Domain.Enums.TenantDatabaseHostingMode.PlatformManaged &&
             database.ProvisioningStatus == Domain.Enums.TenantDatabaseProvisioningStatus.Ready
           orderby database.Id
           select new TenantDatabaseBackupDueCandidate(
             database.Id,
             database.ServerKey,
             database.HostingMode,
             database.ProvisioningStatus,
             policy.ManagementMode,
             policy.Enabled,
             policy.FullBackupIntervalMinutes,
             policy.DifferentialBackupIntervalMinutes,
             policy.TransactionLogBackupIntervalMinutes,
             database.LastSuccessfulFullBackupUtc,
             database.LastSuccessfulDifferentialBackupUtc,
             database.LastSuccessfulLogBackupUtc))
      .Take(take)
      .ToListAsync(cancellationToken);

  public async Task<IReadOnlyDictionary<long, TenantDatabaseBackupRunRecord>> ListLatestRunsAsync(
    IReadOnlyCollection<long> tenantDatabaseIds,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(tenantDatabaseIds);

    if (tenantDatabaseIds.Count == 0)
    {
      return new Dictionary<long, TenantDatabaseBackupRunRecord>();
    }

    // Grouped server-side to the latest StartedUtc per database, then joined back for the row itself. One
    // round trip for the whole due set, served by IX_TenantDatabaseBackupRuns_TenantDatabase_Started.
    var ids = tenantDatabaseIds.Distinct().ToArray();

    var latestStarts = dbContext.TenantDatabaseBackupRuns
      .AsNoTracking()
      .Where(run => ids.Contains(run.TenantDatabaseId))
      .GroupBy(run => run.TenantDatabaseId)
      .Select(group => new { TenantDatabaseId = group.Key, StartedUtc = group.Max(run => run.StartedUtc) });

    var rows = await (from run in dbContext.TenantDatabaseBackupRuns.AsNoTracking()
                      join latest in latestStarts
                        on new { run.TenantDatabaseId, run.StartedUtc }
                        equals new { latest.TenantDatabaseId, latest.StartedUtc }
                      select new TenantDatabaseBackupRunRecord(
                        run.Id,
                        run.TenantDatabaseId,
                        run.Operation.ProviderKey,
                        run.Operation.OperationCode,
                        run.Status,
                        run.StartedUtc,
                        run.CompletedUtc,
                        run.DestinationKey,
                        run.ArtifactReference,
                        run.ProviderBackupIdentity,
                        run.SizeBytes,
                        run.VerificationState,
                        run.LastVerifiedUtc,
                        run.ErrorSummary))
      .ToListAsync(cancellationToken);

    // Two runs can share the exact latest StartedUtc for one database — two scheduler instances starting in
    // the same instant. Either is an equally valid "most recent attempt" for backoff purposes, so the first
    // is taken rather than failing a sweep over a tie.
    var latestPerDatabase = new Dictionary<long, TenantDatabaseBackupRunRecord>();
    foreach (var row in rows)
    {
      latestPerDatabase.TryAdd(row.TenantDatabaseId, row);
    }

    return latestPerDatabase;
  }
}
