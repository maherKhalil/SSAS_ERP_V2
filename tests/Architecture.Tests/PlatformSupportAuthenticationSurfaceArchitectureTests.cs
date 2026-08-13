using System.Reflection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Architecture.Tests;

// Phase 4B durable invariants (DEC-TEN-0023). The platform-support authentication HTTP surface is structurally
// separate from the tenant surface, its current-session logout is bound only from the trusted access token, and
// it does NOT pull in the Phase 4E plane-authentication policy taxonomy (RequirePlatformAuthenticatedUser / D2).
public sealed class PlatformSupportAuthenticationSurfaceArchitectureTests
{
  private const string TenantAuthPrefix = "/api/platform/auth";

  [Fact]
  public void Platform_support_auth_surface_is_structurally_separate_from_the_tenant_surface()
  {
    // Distinct route prefix — neither is a segment-prefix of the other, so the two planes never share a route.
    Assert.Equal("/api/platform/support/auth", PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RoutePrefix);
    Assert.NotEqual(TenantAuthPrefix, PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RoutePrefix);
    Assert.False(PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RoutePrefix
      .StartsWith(TenantAuthPrefix + "/", StringComparison.Ordinal));

    // Distinct HttpOnly refresh cookie name — a tenant refresh cookie is never presentable to the platform surface
    // (different name AND different path scope), and vice versa.
    Assert.NotEqual(
      AuthenticationEndpointRouteBuilderExtensions.RefreshCookieName,
      PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName);
    Assert.Equal("__Secure-ssas-platform-refresh", PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName);

    // The map extension exists (the surface is wired, not latent).
    Assert.NotNull(typeof(PlatformSupportAuthenticationEndpointRouteBuilderExtensions)
      .GetMethod("MapPlatformSupportAuthenticationEndpoints", BindingFlags.Public | BindingFlags.Static));
  }

  [Fact]
  public void Platform_logout_command_binds_only_trusted_token_claims()
  {
    // Both fields are the trusted session_id + identity_id claims; there is no request-body/secret-shaped field a
    // caller could use to target another session. The transport binds them from the validated token, never the body.
    var properties = typeof(RevokeCurrentPlatformAuthenticationSessionCommand).GetProperties();

    Assert.Equal(2, properties.Length);
    Assert.All(properties, property => Assert.Equal(typeof(long), property.PropertyType));
    Assert.Contains(properties, property => property.Name == "PlatformAuthenticationSessionId");
    Assert.Contains(properties, property => property.Name == "IdentityId");
    Assert.DoesNotContain(properties, property =>
      property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Principal", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void Phase_4E_plane_authentication_policy_taxonomy_is_not_pulled_into_phase_4B()
  {
    // Boundary marker: the platform logout route is secured by a narrow inline security_plane=platform check, NOT by
    // the deferred RequirePlatformAuthenticatedUser policy infrastructure (DEC-TEN-0024 / Phase 4E). When 4E lands,
    // it introduces those types and this guard is updated deliberately — it must not appear as a 4B side effect.
    var assemblies = new[]
    {
      typeof(PlatformPermissionAuthorizationHandler).Assembly,                      // Host.API
      typeof(PlatformSupportAuthenticationEndpointRouteBuilderExtensions).Assembly  // Platform.API
    };

    var deferred = assemblies
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.Name.Contains("PlatformAuthenticatedUser", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(deferred);
  }
}
