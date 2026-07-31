namespace SSAS.Platform.Application.Roles;

public sealed record RetireRoleCommand(long RoleId, byte[] ExpectedRowVersion);
