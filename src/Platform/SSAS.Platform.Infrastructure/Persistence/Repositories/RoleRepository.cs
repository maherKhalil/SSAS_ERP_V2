using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Roles;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository(PlatformDbContext dbContext) : IRoleRepository
{
  public Task<Role?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default) =>
    dbContext.Roles
      .Include(role => role.PermissionAssignments)
      .SingleOrDefaultAsync(role => role.Id == roleId, cancellationToken);

  public async Task<IReadOnlyCollection<Role>> GetByIdsAsync(
    IReadOnlyCollection<long> roleIds,
    CancellationToken cancellationToken = default) => await dbContext.Roles
      .Include(role => role.PermissionAssignments)
      .Where(role => roleIds.Contains(role.Id))
      .ToArrayAsync(cancellationToken);

  public Task<bool> NameExistsAsync(
    string normalizedRoleName,
    long? excludingRoleId = null,
    CancellationToken cancellationToken = default) => dbContext.Roles.AnyAsync(
      role => role.NormalizedRoleName == normalizedRoleName && (!excludingRoleId.HasValue || role.Id != excludingRoleId.Value),
      cancellationToken);

  public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
  {
    await dbContext.Roles.AddAsync(role, cancellationToken);
  }
}
