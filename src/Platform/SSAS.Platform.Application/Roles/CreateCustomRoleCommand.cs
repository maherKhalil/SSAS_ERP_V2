namespace SSAS.Platform.Application.Roles;

public sealed record CreateCustomRoleCommand(string Name, string? Description);
