using Microsoft.AspNetCore.Authorization;

namespace SSAS.Host.API.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
  public string Permission { get; } = string.IsNullOrWhiteSpace(permission)
    ? throw new ArgumentException("Permission cannot be null or whitespace.", nameof(permission))
    : permission;
}
