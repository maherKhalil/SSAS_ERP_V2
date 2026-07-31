namespace SSAS.Platform.Application.TenantUsers;

public sealed record RemoveRoleFromTenantUserCommand(long TenantUserId, long RoleId, byte[] ExpectedRowVersion);
