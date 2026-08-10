using Microsoft.AspNetCore.Builder;

namespace SSAS.Platform.API.Transport;

// Thin convention over the existing Host dynamic authorization-policy naming
// (PermissionAuthorizationPolicyProvider materializes "Permission:{name}" into a
// PermissionRequirement). The literal prefix is kept in sync with the Host's
// PermissionAuthorizationDefaults.PolicyPrefix; this class adds no new authorization
// architecture and is tenant-plane (the existing handler enforces trusted tenant + live eligibility).
public static class AdminAuthorizationConventions
{
  public const string PermissionPolicyPrefix = "Permission:";

  public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
    return builder.RequireAuthorization($"{PermissionPolicyPrefix}{permissionName}");
  }
}
