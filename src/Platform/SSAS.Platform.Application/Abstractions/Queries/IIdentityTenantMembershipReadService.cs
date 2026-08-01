namespace SSAS.Platform.Application.Abstractions.Queries;

public interface IIdentityTenantMembershipReadService
{
  Task<IReadOnlyList<EligibleTenantMembership>> ListEligibleMembershipsAsync(
    long identityId,
    CancellationToken cancellationToken = default);

  Task<IdentityTenantMembershipEligibility> GetMembershipEligibilityForUpdateAsync(
    long identityId,
    long tenantUserId,
    Guid tenantId,
    CancellationToken cancellationToken = default);
}
