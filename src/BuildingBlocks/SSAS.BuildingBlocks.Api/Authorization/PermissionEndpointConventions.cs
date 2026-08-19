using Microsoft.AspNetCore.Builder;
using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.BuildingBlocks.Api.Transport;

// THE GENERIC "THIS ENDPOINT REQUIRES PERMISSION X" MECHANISM (FP-006C5).
//
// It expresses a requirement and nothing more. It names no permission, defines no policy and knows no
// module: the CALLER supplies the permission name — Platform passes Platform's, HR passes HR's — and the
// Host's policy provider materialises the requirement. Adding a permission constant here would make one
// module's vocabulary a dependency of every other module's.
public static class PermissionEndpointConventions
{
  // Tenant-plane. The existing handler enforces trusted tenant plus live eligibility; this adds no
  // authorization architecture of its own.
  public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    return builder.RequireAuthorization($"{PermissionPolicyNames.TenantPrefix}{permissionName}");
  }

  // Platform-plane (ADR-015 §8). Deliberately separate from the tenant helper — the two must not mix, and
  // keeping them as two methods is what stops a caller choosing the wrong plane by passing a flag.
  public static TBuilder RequirePlatformPermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    return builder.RequireAuthorization($"{PermissionPolicyNames.PlatformPrefix}{permissionName}");
  }
}
