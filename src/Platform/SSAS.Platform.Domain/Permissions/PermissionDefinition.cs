using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Permissions;

public sealed record PermissionDefinition(PermissionName Name, PermissionScope Scope, string Description);
