namespace SSAS.Platform.Application.Authentication;

public sealed record TenantMembershipSelectionSummary(long TenantUserId, Guid TenantId, string DisplayName);
