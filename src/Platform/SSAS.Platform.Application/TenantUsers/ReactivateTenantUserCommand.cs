namespace SSAS.Platform.Application.TenantUsers;

public sealed record ReactivateTenantUserCommand(long TenantUserId, byte[] ExpectedRowVersion);
