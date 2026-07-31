namespace SSAS.Platform.Application.Roles;

public sealed record RequestRoleRetirementCommand(long RoleId, byte[] ExpectedRowVersion);
