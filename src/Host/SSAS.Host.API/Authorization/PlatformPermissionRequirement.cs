using Microsoft.AspNetCore.Authorization;

namespace SSAS.Host.API.Authorization;

// "This request requires this PlatformSupport permission." Carries only the permission name — no tenant id,
// principal id, session, or security version, and no persistence dependency (ADR-015 §8).
public sealed class PlatformPermissionRequirement(string permission) : IAuthorizationRequirement
{
  public string Permission { get; } = !PlatformPermissionAuthorizationDefaults.IsValidPermissionName(permission)
    ? throw new ArgumentException("Permission names must use dot-separated identifier segments.", nameof(permission))
    : permission;
}
