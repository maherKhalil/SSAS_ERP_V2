using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Roles;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class RoleReadService(PlatformDbContext dbContext) : IRoleReadService
{
  public async Task<RoleDto?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default)
  {
    var role = await dbContext.Roles.AsNoTracking()
      .Include(item => item.PermissionAssignments)
      .SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken);
    return role is null ? null : Map(role);
  }

  public async Task<PagedResult<RoleDto>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
  {
    var query = dbContext.Roles.AsNoTracking().OrderBy(role => role.Id);
    var totalCount = await query.CountAsync(cancellationToken);
    var roles = await query.Include(role => role.PermissionAssignments)
      .Skip((pageNumber - 1) * pageSize)
      .Take(pageSize)
      .ToArrayAsync(cancellationToken);
    return new PagedResult<RoleDto>(roles.Select(Map).ToArray(), pageNumber, pageSize, totalCount);
  }

  private static RoleDto Map(Domain.Roles.Role role) => new(
    role.Id,
    role.Name.Value,
    role.Description,
    role.RoleType,
    role.Status,
    role.ActivePermissions.Select(permission => permission.Value).ToArray(),
    role.RowVersion.ToArray());
}
