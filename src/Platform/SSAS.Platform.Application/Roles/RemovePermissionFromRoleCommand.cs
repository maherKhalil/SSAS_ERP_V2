namespace SSAS.Platform.Application.Roles;

public sealed record RemovePermissionFromRoleCommand(long RoleId, string PermissionName, byte[] ExpectedRowVersion);
