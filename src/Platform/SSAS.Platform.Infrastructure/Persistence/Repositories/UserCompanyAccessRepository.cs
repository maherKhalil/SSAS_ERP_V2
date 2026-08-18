using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Company assignment rows on the platform plane (FP-006C1, ADR-025 decision 5).
//
// Every query states TenantId explicitly. UserCompanyAccess is deliberately not tenant-owned — it is read on
// paths that resolve scope before an ambient tenant is guaranteed — so the predicate is the only thing
// keeping one tenant's assignments out of another's answer.
internal sealed class UserCompanyAccessRepository(PlatformDbContext dbContext) : IUserCompanyAccessRepository
{
  public async Task<IReadOnlyList<Guid>> GetCompanyIdsAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default) =>
    await dbContext.UserCompanyAccess
      .AsNoTracking()
      .Where(access => access.TenantId == tenantId && access.TenantUserId == tenantUserId)
      .Select(access => access.CompanyId)
      .ToListAsync(cancellationToken);

  public async Task AddAsync(UserCompanyAccess access, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(access);
    await dbContext.UserCompanyAccess.AddAsync(access, cancellationToken);
  }

  // Tracked-then-removed rather than ExecuteDelete, so the removal joins the caller's unit of work and
  // commits or rolls back with the rest of the replacement instead of landing on its own.
  public async Task RemoveAsync(
    Guid tenantId,
    long tenantUserId,
    IReadOnlyCollection<Guid> companyIds,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(companyIds);

    if (companyIds.Count == 0)
    {
      return;
    }

    var doomed = await dbContext.UserCompanyAccess
      .Where(access => access.TenantId == tenantId &&
        access.TenantUserId == tenantUserId &&
        companyIds.Contains(access.CompanyId))
      .ToListAsync(cancellationToken);

    dbContext.UserCompanyAccess.RemoveRange(doomed);
  }
}
