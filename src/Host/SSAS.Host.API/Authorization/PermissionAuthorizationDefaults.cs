using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.Host.API.Authorization;

public static class PermissionAuthorizationDefaults
{
  // THE CANONICAL SPELLING, read from the one shared contract rather than repeated here (FP-006C5).
  public const string PolicyPrefix = PermissionPolicyNames.TenantPrefix;

  public static string CreatePolicyName(string permission)
  {
    if (!IsValidPermissionName(permission))
    {
      throw new ArgumentException("Permission names must use dot-separated identifier segments.", nameof(permission));
    }

    return $"{PolicyPrefix}{permission}";
  }

  public static bool TryGetPermissionName(string? policyName, out string permission)
  {
    permission = string.Empty;

    if (policyName is null || !policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
    {
      return false;
    }

    var candidate = policyName[PolicyPrefix.Length..];
    if (!IsValidPermissionName(candidate))
    {
      return false;
    }

    permission = candidate;
    return true;
  }

  public static bool IsValidPermissionName(string? permission) => AuthorizationNameValidator.IsValid(permission);
}
