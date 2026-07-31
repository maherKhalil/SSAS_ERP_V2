namespace SSAS.Platform.Application.TenantUsers;

public sealed record DeactivateTenantUserCommand(long TenantUserId, byte[] ExpectedRowVersion);
