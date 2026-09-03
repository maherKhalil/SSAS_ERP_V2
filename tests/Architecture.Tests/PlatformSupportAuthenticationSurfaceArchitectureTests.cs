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

  // ⚠⚠⚠ THIS GUARD IS THE EVIDENCE THAT `AC-TEN-0084`, `0085` AND `0086` ARE **DEFERRED, NOT UNCOVERED**,
  // AND IT CORRECTS A PARTITION I PUBLISHED. I classified `0021`-`0091` by cluster and put `0084`-`0091` in
  // the LIVE bucket on the strength of routes and policies existing. **The Phase-4E plane-authentication
  // POLICY TAXONOMY does not exist**, and this test is what says so: no type named `PlatformAuthenticatedUser`
  // is present in Host.API or Platform.API, and the platform logout route is secured by a narrow inline
  // `security_plane=platform` check rather than by the deferred policy infrastructure (`DEC-TEN-0024`).
  //
  //   `AC-TEN-0084` *"tenant-authenticated, PLATFORM-AUTHENTICATED … and plane-neutral policies EXIST"*
  //   `AC-TEN-0085` *"an architecture guard rejects a plane-specific endpoint using a bare
  //                  `RequireAuthenticatedUser`"* — needs the taxonomy to have something to reject
  //   `AC-TEN-0086` *"…require the TENANT-AUTHENTICATED POLICY"* — names a policy that is not built
  //
  // **All three name the taxonomy this guard proves absent**, so their subjects are Phase 4E. My partition's
  // deferred bucket was 6 and is 9. ⚠ I flagged this risk when I published it — *"I classified at CLUSTER
  // granularity and did not open all 62 individually"* — and this is that risk landing.
  //
  // ⚠⚠ `AC-TEN-0085` IS ALSO THE ONLY CRITERION IN THE PACKAGE WHOSE SUBJECT IS A TEST. It does not describe
  // product behaviour; it requires that an ARCHITECTURE GUARD exist. **When 4E lands, satisfying `0085` means
  // writing a guard — and the criterion is closed by the guard's existence, not by anything the guard finds.**
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

    // ⚠ THREE TESTS HERE PASSED OVER AN EMPTY TYPE SET (T-258). The floor is on the scanned types,
    // not on the deferred ones — an empty deferred list is the success condition.
    var scanned = assemblies.SelectMany(assembly => assembly.GetTypes()).ToArray();
    Assert.True(scanned.Length >= 20,
      $"only {scanned.Length} types were scanned across the assemblies; the enumeration collapsed.");

    // ⚠ THE CONTROL ON THE MATCHER (T-263). The floor proves types were scanned. It cannot prove the name
    // test still selects anything, and a ban whose matcher matches nothing is green for the wrong reason.
    // So the same `Name.Contains` is run over the same collection for a term that MUST be present.
    Assert.Contains(scanned, type => type.Name.Contains("PlatformSupport", StringComparison.Ordinal));

    var deferred = scanned
      .Where(type => type.Name.Contains("PlatformAuthenticatedUser", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(deferred);
  }
}
