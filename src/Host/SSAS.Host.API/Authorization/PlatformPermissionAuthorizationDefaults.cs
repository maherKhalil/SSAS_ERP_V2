using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.Host.API.Authorization;

// Platform-plane dynamic authorization-policy naming (ADR-015 §8 / DEC-TEN-0022). Structurally distinct from
// the tenant "Permission:" prefix so a platform-support permission can never be satisfied through the tenant
// policy path and vice versa. "PlatformPermission:" is not a prefix of "Permission:" (or the reverse), so the
// two never collide in the shared policy provider.
public static class PlatformPermissionAuthorizationDefaults
{
  // THE CANONICAL SPELLING, read from the one shared contract rather than repeated here (FP-006C5).
  public const string PolicyPrefix = PermissionPolicyNames.PlatformPrefix;

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
