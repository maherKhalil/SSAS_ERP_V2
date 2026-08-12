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

  // Platform-plane policy prefix, kept in sync with the Host's PlatformPermissionAuthorizationDefaults.PolicyPrefix
  // (Platform.API cannot reference the Host assembly, so the literal is duplicated exactly like the tenant prefix).
  public const string PlatformPermissionPolicyPrefix = "PlatformPermission:";

  public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
    return builder.RequireAuthorization($"{PermissionPolicyPrefix}{permissionName}");
  }

  // Platform-plane authority endpoints (ADR-015 §8 / Phase 4). Emits the "PlatformPermission:" policy so the
  // request is authorized by the platform handler (validated platform plane + PlatformSupport-scoped permission),
  // never the tenant "Permission:" path. Deliberately separate from RequirePermission — the two must not mix.
  public static TBuilder RequirePlatformPermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
    return builder.RequireAuthorization($"{PlatformPermissionPolicyPrefix}{permissionName}");
  }
}
