namespace SSAS.Platform.Application.Roles;

public sealed record AssignPermissionToRoleCommand(long RoleId, string PermissionName, byte[] ExpectedRowVersion);
