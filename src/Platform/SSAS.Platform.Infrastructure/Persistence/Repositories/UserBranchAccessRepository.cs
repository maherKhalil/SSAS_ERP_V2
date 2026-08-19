using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Branches;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Branch assignment rows on the platform plane (Branch foundation B1b).
//
// Every query states TenantId explicitly. UserBranchAccess is deliberately not tenant-owned — the
// authentication path reads it before an ambient tenant exists — so the predicate is the only thing keeping
// one tenant's assignments out of another's answer.
internal sealed class UserBranchAccessRepository(PlatformDbContext dbContext) : IUserBranchAccessRepository
{
  public async Task<IReadOnlyList<Guid>> GetBranchIdsAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default) =>
    await dbContext.UserBranchAccess
      .AsNoTracking()
      .Where(access => access.TenantId == tenantId && access.TenantUserId == tenantUserId)
      .Select(access => access.BranchId)
      .ToListAsync(cancellationToken);

  public async Task AddAsync(UserBranchAccess access, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(access);
    await dbContext.UserBranchAccess.AddAsync(access, cancellationToken);
  }

  // Tracked-then-removed rather than ExecuteDelete, so the removal joins the caller's unit of work and
  // commits or rolls back with the rest of the replacement instead of landing on its own.
  public async Task RemoveAsync(
    Guid tenantId,
    long tenantUserId,
    IReadOnlyCollection<Guid> branchIds,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(branchIds);

    if (branchIds.Count == 0)
    {
      return;
    }

    var doomed = await dbContext.UserBranchAccess
      .Where(access => access.TenantId == tenantId &&
        access.TenantUserId == tenantUserId &&
        branchIds.Contains(access.BranchId))
      .ToListAsync(cancellationToken);

    dbContext.UserBranchAccess.RemoveRange(doomed);
  }
}
