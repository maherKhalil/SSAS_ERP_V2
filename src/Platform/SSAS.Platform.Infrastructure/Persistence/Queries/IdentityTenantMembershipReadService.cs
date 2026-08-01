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
      .ToListAsync(cancellationToken);
    var eligible = new List<EligibleTenantMembership>(memberships.Count);
    foreach (var membership in memberships)
    {
      var tenant = await tenantEligibilityReadService.GetEligibilityAsync(membership.TenantId, cancellationToken);
      if (tenant.IsAuthenticationEligible)
      {
        eligible.Add(new EligibleTenantMembership(
          membership.IdentityId,
          membership.Id,
          membership.TenantId,
          membership.DisplayName.Value));
      }
    }

    return eligible;
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
    var projection = new EligibleTenantMembership(
      membership.IdentityId,
      membership.Id,
      membership.TenantId,
      membership.DisplayName.Value);
    return new IdentityTenantMembershipEligibility(projection, tenant.IsAuthenticationEligible);
  }
}
