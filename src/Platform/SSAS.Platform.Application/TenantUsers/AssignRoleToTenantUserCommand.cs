namespace SSAS.Platform.Application.TenantUsers;

public sealed record AssignRoleToTenantUserCommand(long TenantUserId, long RoleId, byte[] ExpectedRowVersion);
