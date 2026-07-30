using Microsoft.AspNetCore.Authorization;

namespace SSAS.Host.API.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
  public string Permission { get; } = !PermissionAuthorizationDefaults.IsValidPermissionName(permission)
    ? throw new ArgumentException("Permission names must use dot-separated identifier segments.", nameof(permission))
    : permission;
}
