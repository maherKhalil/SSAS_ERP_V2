using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

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
