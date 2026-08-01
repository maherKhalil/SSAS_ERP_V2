namespace SSAS.Platform.Application.Abstractions.Queries;

public sealed record EligibleTenantMembership(
  long IdentityId,
  long TenantUserId,
  Guid TenantId,
  string TenantDisplayName);
