namespace SSAS.Platform.Application.Abstractions.Queries;

public sealed record IdentityTenantMembershipEligibility(
  EligibleTenantMembership? Membership,
  bool IsTenantEligible)
{
  public bool IsEligible => Membership is not null && IsTenantEligible;
}
