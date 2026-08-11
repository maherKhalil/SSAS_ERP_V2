using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class AccessTokenClaimsProvider(PlatformDbContext dbContext, IPermissionCatalog permissionCatalog)
  : IAccessTokenClaimsProvider
{
  public async Task<Result<AccessTokenClaims>> GetClaimsAsync(
    long authenticationSessionId,
    long identityId,
    long tenantUserId,
    Guid tenantId,
    AuthenticationClientId clientId,
    long securityVersion,
    CancellationToken cancellationToken = default)
  {
    if (authenticationSessionId <= 0 || identityId <= 0 || tenantUserId <= 0 || tenantId == Guid.Empty ||
      clientId is null || securityVersion <= 0)
    {
      return Invalid();
    }

    var binding = await dbContext.AuthenticationSessions.AsNoTracking()
      .Where(session =>
        session.Id == authenticationSessionId &&
        session.IdentityId == identityId &&
        session.TenantUserId == tenantUserId &&
        session.TenantId == tenantId &&
        session.ClientId == clientId.Value &&
        session.SecurityVersionAtCreation == securityVersion &&
        session.Status == AuthenticationSessionStatus.Active)
      .Join(
        dbContext.Identities.AsNoTracking(),
        session => session.IdentityId,
        identity => identity.Id,
        (session, identity) => new { Session = session, Identity = identity })
      .Join(
        dbContext.TenantUsers.IgnoreQueryFilters().AsNoTracking()
          .Where(user => user.Status == TenantUserStatus.Active),
        item => new { item.Session.TenantId, item.Session.TenantUserId, item.Session.IdentityId },
        user => new { user.TenantId, TenantUserId = user.Id, user.IdentityId },
        (item, user) => new { item.Session, item.Identity, User = user })
      .Join(
        dbContext.Tenants.AsNoTracking().Where(tenant => tenant.Status == TenantStatus.Active),
        item => item.Session.TenantId,
        tenant => tenant.Id,
        (item, _) => item)
      .SingleOrDefaultAsync(cancellationToken);
    if (binding is null)
    {
      return Invalid();
    }

    var roleRows = await dbContext.TenantUserRoleAssignments.IgnoreQueryFilters().AsNoTracking()
      .Where(assignment =>
        assignment.TenantId == tenantId &&
        assignment.TenantUserId == tenantUserId &&
        assignment.RemovedUtc == null)
      .Join(
        dbContext.Roles.IgnoreQueryFilters().AsNoTracking()
          .Where(role => role.TenantId == tenantId && role.Status != RoleStatus.Retired),
        assignment => new { assignment.TenantId, assignment.RoleId },
        role => new { role.TenantId, RoleId = role.Id },
        (_, role) => role)
      .ToListAsync(cancellationToken);

    var roleIds = roleRows.Select(role => role.Id).Distinct().ToArray();
    var permissions = await dbContext.RolePermissionAssignments.IgnoreQueryFilters().AsNoTracking()
        .Where(assignment =>
          assignment.TenantId == tenantId &&
          roleIds.Contains(assignment.RoleId) &&
          assignment.RemovedUtc == null)
        .Select(assignment => assignment.PermissionName)
        .ToListAsync(cancellationToken);

    // Defense in depth (ADR-015 / AC-TEN-0030): tenant tokens emit only tenant-scoped permissions,
    // so a corrupt or force-seeded PlatformSupport assignment can never become a tenant-token claim.
    var tenantScopedPermissions = TenantPermissionClaimFilter.FilterToTenantScope(
      permissions.Select(permission => permission.Value),
      permissionCatalog);

    return Result.Success(new AccessTokenClaims(
      binding.Identity.Subject.Value,
      identityId,
      tenantId,
      tenantUserId,
      authenticationSessionId,
      clientId,
      securityVersion,
      roleRows.Select(role => role.Name.Value).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
      tenantScopedPermissions.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()));
  }

  private static Result<AccessTokenClaims> Invalid() =>
    Result.Failure<AccessTokenClaims>(AuthenticationErrors.InvalidAccessTokenClaims);
}
