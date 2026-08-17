using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// Resolves Platform.Tenant.Administer through the user's ACTIVE roles and their ACTIVE permission
// assignments, in one query (Branch foundation B0/B1).
//
// EVERY "STILL VALID" CONDITION IS IN THE PREDICATE, not assumed by the caller: the membership must be
// Active, the role assignment must not be removed, the role must not be retired, and the permission grant
// must not be revoked. Any one of those being stale is the difference between an administrator and a former
// administrator, and this is asked on the authorization path where that distinction is the whole point.
//
// TenantId is compared explicitly. These rows are not tenant-filtered by the global filter — deliberately,
// so authentication can read them before an ambient tenant exists — which makes the predicate the only
// thing keeping one tenant's roles from answering for another's user.
internal sealed class TenantAdministratorAuthority(PlatformDbContext dbContext) : ITenantAdministratorAuthority
{
  public Task<bool> IsTenantAdministratorAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default) =>
    tenantId == Guid.Empty || tenantUserId <= 0
      ? Task.FromResult(false)
      : dbContext.TenantUsers
        .AsNoTracking()
        .Where(user => user.TenantId == tenantId &&
          user.Id == tenantUserId &&
          user.Status == TenantUserStatus.Active)
        .SelectMany(user => user.RoleAssignments.Where(assignment => assignment.RemovedUtc == null))
        .Join(
          dbContext.Roles.Where(role => role.Status != RoleStatus.Retired),
          assignment => new { assignment.TenantId, RoleId = assignment.RoleId },
          role => new { role.TenantId, RoleId = role.Id },
          (assignment, role) => role)
        .SelectMany(role => role.PermissionAssignments)
        .AnyAsync(
          permission => permission.RemovedUtc == null &&
            permission.PermissionName.Value == PlatformPermissionNames.AdministerTenant,
          cancellationToken);
}
