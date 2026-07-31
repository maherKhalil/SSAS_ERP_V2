namespace SSAS.Platform.Application.TenantUsers;

public sealed record CreateTenantUserMembershipCommand(long IdentityId, string Email, string DisplayName);
