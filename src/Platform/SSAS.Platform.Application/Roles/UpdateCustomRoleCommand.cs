namespace SSAS.Platform.Application.Roles;

public sealed record UpdateCustomRoleCommand(long RoleId, string Name, string? Description, byte[] ExpectedRowVersion);
