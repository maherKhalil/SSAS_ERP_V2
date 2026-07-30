namespace SSAS.Host.API.Authorization;

public static class RoleAuthorizationDefaults
{
  public const string PolicyPrefix = "Role:";

  public static string CreatePolicyName(string role)
  {
    if (!IsValidRoleName(role))
    {
      throw new ArgumentException("Role names must use dot-separated identifier segments.", nameof(role));
    }

    return $"{PolicyPrefix}{role}";
  }

  public static bool TryGetRoleName(string? policyName, out string role)
  {
    role = string.Empty;

    if (policyName is null || !policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
    {
      return false;
    }

    var candidate = policyName[PolicyPrefix.Length..];
    if (!IsValidRoleName(candidate))
    {
      return false;
    }

    role = candidate;
    return true;
  }

  public static bool IsValidRoleName(string? role) => AuthorizationNameValidator.IsValid(role);
}
