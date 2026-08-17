using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// Resolves Platform.Tenant.Administer through the user's ACTIVE roles and their ACTIVE permission grants
// (Branch foundation B0/B1).
//
// EVERY "STILL VALID" CONDITION IS IN THE PREDICATE, not assumed by the caller: the membership must be
// Active, the role assignment must not be removed, the role must not be retired, and the permission grant
// must not be revoked. Any one of those being stale is the difference between an administrator and a former
// administrator, and this is asked on the authorization path where that distinction is the whole point.
//
// ---- THE GLOBAL TENANT FILTER IS BYPASSED DELIBERATELY, AND REPLACED BY AN EXPLICIT PREDICATE.
//
// TenantUser, Role and both assignment tables are tenant-owned, so the filter keys on the AMBIENT tenant —
// which does not exist yet at the earliest call site: authentication asks this while deciding whether a
// login completes, before any request-scoped tenant context is established. Left filtered, the query would
// return nothing there and silently answer "not an administrator" for every real administrator.
//
// Safe because the tenant is never inferred but always STATED: every clause compares TenantId to the
// caller's argument, so bypassing the ambient filter widens nothing.
//
// TWO ROUND TRIPS RATHER THAN ONE JOIN. Composing the navigation collections into a single query produces
// an expression EF cannot translate, and a client-evaluated authorization check is worse than a second
// seek: both queries below are covered by existing keys and the second runs only when the first found
// roles at all.
internal sealed class TenantAdministratorAuthority(PlatformDbContext dbContext) : ITenantAdministratorAuthority
{
  public async Task<bool> IsTenantAdministratorAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0)
    {
      return false;
    }

    var membershipIsActive = await dbContext.TenantUsers
      .IgnoreQueryFilters()
      .AsNoTracking()
      .AnyAsync(
        user => user.TenantId == tenantId && user.Id == tenantUserId && user.Status == TenantUserStatus.Active,
        cancellationToken);
    if (!membershipIsActive)
    {
      return false;
    }

    // The roles this user currently holds, excluding removed assignments and retired roles.
    var roleIds = await dbContext.Set<TenantUserRoleAssignment>()
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == tenantId &&
        assignment.TenantUserId == tenantUserId &&
        assignment.RemovedUtc == null)
      .Select(assignment => assignment.RoleId)
      .ToListAsync(cancellationToken);

    if (roleIds.Count == 0)
    {
      return false;
    }

    var liveRoleIds = await dbContext.Roles
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(role => role.TenantId == tenantId && roleIds.Contains(role.Id) && role.Status != RoleStatus.Retired)
      .Select(role => role.Id)
      .ToListAsync(cancellationToken);

    if (liveRoleIds.Count == 0)
    {
      return false;
    }

    // Compared as the value object so EF applies the configured conversion rather than reaching through it.
    var administerTenant = PermissionName.Create(PlatformPermissionNames.AdministerTenant).Value;

    return await dbContext.Set<RolePermissionAssignment>()
      .IgnoreQueryFilters()
      .AsNoTracking()
      .AnyAsync(
        grant => grant.TenantId == tenantId &&
          liveRoleIds.Contains(grant.RoleId) &&
          grant.RemovedUtc == null &&
          grant.PermissionName == administerTenant,
        cancellationToken);
  }
}
