using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantUserRepository(PlatformDbContext dbContext) : ITenantUserRepository
{
  public Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default) =>
    dbContext.TenantUsers
      .Include(user => user.RoleAssignments)
      .SingleOrDefaultAsync(user => user.Id == tenantUserId, cancellationToken);

  public Task<bool> EmailExistsAsync(
    string normalizedEmail,
    long? excludingTenantUserId = null,
    CancellationToken cancellationToken = default) => dbContext.TenantUsers.AnyAsync(
      user => user.NormalizedEmail == normalizedEmail && (!excludingTenantUserId.HasValue || user.Id != excludingTenantUserId.Value),
      cancellationToken);

  public Task<bool> MembershipExistsAsync(long identityId, CancellationToken cancellationToken = default) =>
    dbContext.TenantUsers.AnyAsync(user => user.IdentityId == identityId, cancellationToken);

  public Task<bool> HasActiveAssignmentToRoleAsync(long roleId, CancellationToken cancellationToken = default) =>
    dbContext.TenantUserRoleAssignments
      .Where(assignment => assignment.RoleId == roleId && assignment.RemovedUtc == null)
      .Join(
        dbContext.TenantUsers.Where(user => user.Status == TenantUserStatus.Active),
        assignment => new { assignment.TenantId, assignment.TenantUserId },
        user => new { user.TenantId, TenantUserId = user.Id },
        (_, _) => 1)
      .AnyAsync(cancellationToken);

  public async Task AddAsync(TenantUser tenantUser, CancellationToken cancellationToken = default)
  {
    await dbContext.TenantUsers.AddAsync(tenantUser, cancellationToken);
  }
}
