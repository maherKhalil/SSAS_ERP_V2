using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class TenantUserReadService(PlatformDbContext dbContext) : ITenantUserReadService
{
  public async Task<TenantUserDto?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default)
  {
    var user = await dbContext.TenantUsers.AsNoTracking()
      .Include(item => item.RoleAssignments)
      .SingleOrDefaultAsync(item => item.Id == tenantUserId, cancellationToken);
    return user is null ? null : Map(user);
  }

  public async Task<PagedResult<TenantUserDto>> ListAsync(
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default)
  {
    var query = dbContext.TenantUsers.AsNoTracking().OrderBy(user => user.Id);
    var totalCount = await query.CountAsync(cancellationToken);
    var users = await query.Include(user => user.RoleAssignments)
      .Skip((pageNumber - 1) * pageSize)
      .Take(pageSize)
      .ToArrayAsync(cancellationToken);
    return new PagedResult<TenantUserDto>(users.Select(Map).ToArray(), pageNumber, pageSize, totalCount);
  }

  private static TenantUserDto Map(Domain.TenantUsers.TenantUser user) => new(
    user.Id,
    user.IdentityId,
    user.Email.Value,
    user.DisplayName.Value,
    user.Status,
    user.ActiveRoleIds.ToArray(),
    user.RowVersion.ToArray());
}
