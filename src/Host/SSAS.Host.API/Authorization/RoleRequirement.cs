using Microsoft.AspNetCore.Authorization;

namespace SSAS.Host.API.Authorization;

public sealed class RoleRequirement(string role) : IAuthorizationRequirement
{
  public string Role { get; } = !RoleAuthorizationDefaults.IsValidRoleName(role)
    ? throw new ArgumentException("Role names must use dot-separated identifier segments.", nameof(role))
    : role;
}
