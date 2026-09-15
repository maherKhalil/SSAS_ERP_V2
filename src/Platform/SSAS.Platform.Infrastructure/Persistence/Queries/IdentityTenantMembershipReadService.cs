using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// CROSS-TENANT BY DESIGN.
//
// This service answers *which tenants does this identity belong to*, which is asked BEFORE a tenant is
// selected and therefore has no tenant to scope to. `IgnoreQueryFilters()` below is required rather than
// tolerated: the ambient `CurrentTenantId` is null at this point, and the global filter
// (`PersistenceDbContext.ConfigureTenantFilter`) fails CLOSED — so with the filter applied this query
// would return no memberships at all and tenant selection could never begin.
//
// ⚠ The scope here is the IDENTITY, not a tenant: `user.IdentityId == identityId` plus an Active status on
// both sides. **A `TenantId ==` predicate would be wrong, not missing** — it would reduce the answer to the
// one tenant the caller has not chosen yet.
//
// ⚠⚠ `PlatformReadScopeArchitectureTests` requires every Platform read service that ignores the global
// filter to carry EITHER a hand-written `TenantId ==` predicate OR this marker. **The marker is the
// grounds, and it is read by a human**: do not add it elsewhere to quieten that test — a read that should
// be tenant-scoped needs the predicate instead.
public sealed class IdentityTenantMembershipReadService(
  PlatformDbContext dbContext,
  ITenantAuthenticationEligibilityReadService tenantEligibilityReadService)
  : IIdentityTenantMembershipReadService
{
  public async Task<IReadOnlyList<EligibleTenantMembership>> ListEligibleMembershipsAsync(
    long identityId,
    CancellationToken cancellationToken = default)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identityId);
    var memberships = await dbContext.TenantUsers
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(user => user.IdentityId == identityId && user.Status == TenantUserStatus.Active)
      .Join(
        dbContext.Tenants.AsNoTracking().Where(tenant => tenant.Status == TenantStatus.Active),
        user => user.TenantId,
        tenant => tenant.Id,
        (user, tenant) => new { User = user, Tenant = tenant })
      .ToListAsync(cancellationToken);
    return memberships
      .Select(item => new EligibleTenantMembership(
        item.User.IdentityId,
        item.User.Id,
        item.User.TenantId,
        item.Tenant.TenantName.Value))
      .ToArray();
  }

  public async Task<IdentityTenantMembershipEligibility> GetMembershipEligibilityForUpdateAsync(
    long identityId,
    long tenantUserId,
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identityId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantUserId);
    if (tenantId == Guid.Empty)
    {
      throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
    }

    var membership = await dbContext.TenantUsers
      .FromSqlInterpolated($"SELECT * FROM [platform].[TenantUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId} AND [TenantUserId] = {tenantUserId} AND [TenantId] = {tenantId} AND [Status] = N'Active'")
      .IgnoreQueryFilters()
      .SingleOrDefaultAsync(cancellationToken);
    if (membership is null)
    {
      return new IdentityTenantMembershipEligibility(null, false);
    }

    var tenant = await tenantEligibilityReadService.GetEligibilityForUpdateAsync(tenantId, cancellationToken);
    var tenantEntity = dbContext.Tenants.Local.SingleOrDefault(item => item.Id == tenantId);
    var projection = new EligibleTenantMembership(
      membership.IdentityId,
      membership.Id,
      membership.TenantId,
      tenantEntity?.TenantName.Value ?? string.Empty);
    return new IdentityTenantMembershipEligibility(projection, tenant.IsAuthenticationEligible);
  }
}
