using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Backup policy and run-history reads (ADR-022), projected to flat records so no EF entity escapes into the
// Application layer.
//
// Everything here is keyed on the PHYSICAL TenantDatabase, never on a tenant or an assignment: a shared
// database is one backup target regardless of how many tenants it hosts, and querying by assignment would
// return the same chain once per tenant.
public sealed class TenantDatabaseBackupReadRepository(PlatformDbContext dbContext)
  : ITenantDatabaseBackupReadRepository
{
  public Task<TenantDatabaseBackupPolicyRecord?> FindPolicyAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default) =>
    // SingleOrDefaultAsync rather than FirstOrDefaultAsync: UX_TenantDatabaseBackupPolicies_TenantDatabase
    // makes two policies for one physical database structurally impossible, so if one ever appears this
    // throws rather than quietly picking a protection configuration.
    dbContext.TenantDatabaseBackupPolicies
      .AsNoTracking()
      .Where(policy => policy.TenantDatabaseId == tenantDatabaseId)
      .Select(policy => new TenantDatabaseBackupPolicyRecord(
        policy.Id,
        policy.TenantDatabaseId,
        policy.Enabled,
        policy.ManagementMode,
        policy.DestinationKey,
        policy.FullBackupIntervalMinutes,
        policy.DifferentialBackupIntervalMinutes,
        policy.TransactionLogBackupIntervalMinutes,
        policy.RetentionExpectationDays,
        policy.RestoreVerificationIntervalDays,
        policy.MaximumBackupAgeMinutes,
        policy.CompressionMode,
        policy.EncryptionMode))
      .SingleOrDefaultAsync(cancellationToken);

  public async Task<IReadOnlyList<TenantDatabaseBackupRunRecord>> ListRecentRunsAsync(
    long tenantDatabaseId,
    int take,
    CancellationToken cancellationToken = default) =>
    await dbContext.TenantDatabaseBackupRuns
      .AsNoTracking()
      .Where(run => run.TenantDatabaseId == tenantDatabaseId)
      .OrderByDescending(run => run.StartedUtc)
      .ThenByDescending(run => run.Id)
      .Take(take)
      .Select(Projection)
      .ToListAsync(cancellationToken);

  public Task<TenantDatabaseBackupRunRecord?> FindLatestSuccessfulRunAsync(
    long tenantDatabaseId,
    string operationProviderKey,
    string operationCode,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantDatabaseBackupRuns
      .AsNoTracking()
      .Where(run =>
        run.TenantDatabaseId == tenantDatabaseId &&
        run.Operation.ProviderKey == operationProviderKey &&
        run.Operation.OperationCode == operationCode &&
        run.Status == Domain.Enums.TenantDatabaseBackupRunStatus.Succeeded)
      .OrderByDescending(run => run.StartedUtc)
      .ThenByDescending(run => run.Id)
      .Select(Projection)
      .FirstOrDefaultAsync(cancellationToken);

  public Task<TenantDatabaseRecoveryEvidenceRecord?> FindRecoveryEvidenceAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default) =>
    dbContext.Set<Domain.TenantStorage.TenantDatabase>()
      .AsNoTracking()
      .Where(database => database.Id == tenantDatabaseId)
      .Select(database => new TenantDatabaseRecoveryEvidenceRecord(
        database.Id,
        database.LastSuccessfulFullBackupUtc,
        database.LastSuccessfulDifferentialBackupUtc,
        database.LastSuccessfulLogBackupUtc,
        database.LastRestoreVerificationUtc))
      .FirstOrDefaultAsync(cancellationToken)!;

  // A shared projection EXPRESSION rather than a method: EF must translate it to SQL, and a method call
  // would force client-side evaluation of the whole entity.
  private static readonly System.Linq.Expressions.Expression<
    Func<Domain.TenantStorage.TenantDatabaseBackupRun, TenantDatabaseBackupRunRecord>> Projection =
    run => new TenantDatabaseBackupRunRecord(
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
      run.ErrorSummary);
}
