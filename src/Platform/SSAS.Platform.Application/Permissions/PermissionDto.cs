using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Permissions;

public sealed record PermissionDto(string Name, PermissionScope Scope, string Description);
