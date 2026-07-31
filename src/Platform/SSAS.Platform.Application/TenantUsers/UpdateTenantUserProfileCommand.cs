namespace SSAS.Platform.Application.TenantUsers;

public sealed record UpdateTenantUserProfileCommand(long TenantUserId, string Email, string DisplayName, byte[] ExpectedRowVersion);
