using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authorization;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Infrastructure;

// Phase 4A platform-plane authorization primitives (ADR-015 §8 / DEC-TEN-0022). These prove the handler is
// fail-closed and stateless (claims + code-owned catalog only), and that the dynamic policy provider / endpoint
// convention keep the platform prefix structurally separate from the tenant "Permission:" / "Role:" prefixes.
public sealed class PlatformPermissionAuthorizationTests
{
  private const string Administer = PlatformPermissionNames.AdministerPlatformSupport; // PlatformSupport scope
  private const string ViewTenants = PlatformPermissionNames.ViewTenants;              // PlatformSupport scope
  private const string ViewCompanies = PlatformPermissionNames.ViewCompanies;          // Tenant scope (exists in catalog)

  // ---- Handler: success ----

  [Fact]
  public async Task Platform_token_with_the_required_platform_support_permission_is_authorized()
  {
    var context = PlatformContext(Administer,
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.Permission, Administer));

    await Handler().HandleAsync(context);

    Assert.True(context.HasSucceeded);
  }

  // ---- Handler: plane fail-closed ----

  [Fact]
  public async Task Platform_token_missing_the_required_permission_is_denied()
  {
    var context = PlatformContext(Administer, new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task A_tenant_token_carrying_the_same_permission_text_is_denied()
  {
    // No security_plane=platform, so even an (illegally) forged matching permission claim cannot satisfy it.
    var context = PlatformContext(Administer,
      new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString()),
      new Claim(JwtClaimTypes.Permission, Administer));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task A_legacy_token_with_no_security_plane_is_denied()
  {
    var context = PlatformContext(Administer, new Claim(JwtClaimTypes.Permission, Administer));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Theory]
  [InlineData(SecurityPlane.Tenant)]
  [InlineData("Platform")]  // wrong case
  [InlineData("PLATFORM")]  // wrong case
  [InlineData("")]          // blank
  [InlineData("   ")]       // whitespace
  [InlineData("bogus")]     // unknown
  public async Task A_non_exact_platform_plane_value_is_denied(string plane)
  {
    var context = PlatformContext(Administer,
      new Claim(JwtClaimTypes.SecurityPlane, plane),
      new Claim(JwtClaimTypes.Permission, Administer));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task A_duplicate_security_plane_claim_is_denied()
  {
    var context = PlatformContext(Administer,
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.Permission, Administer));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task An_unauthenticated_principal_is_denied()
  {
    // No authentication type => ClaimsIdentity.IsAuthenticated is false even with the right claims.
    var context = new AuthorizationHandlerContext(
      [new PlatformPermissionRequirement(Administer)],
      new ClaimsPrincipal(new ClaimsIdentity(
      [
        new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
        new Claim(JwtClaimTypes.Permission, Administer)
      ])),
      resource: null);

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  // ---- Handler: catalog / scope fail-closed ----

  [Fact]
  public async Task An_unknown_permission_is_denied()
  {
    const string unknown = "Platform.Support.DoesNotExist";
    var context = PlatformContext(unknown,
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.Permission, unknown));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task A_tenant_scoped_permission_name_cannot_be_satisfied_by_a_platform_token()
  {
    // ViewCompanies exists in the catalog but is PermissionScope.Tenant — a platform policy must reject it.
    var context = PlatformContext(ViewCompanies,
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.Permission, ViewCompanies));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task A_lower_privilege_platform_permission_does_not_imply_a_higher_one()
  {
    // Token holds Platform.Tenants.View but the requirement is Platform.Support.Administer — no hierarchy.
    var context = PlatformContext(Administer,
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.Permission, ViewTenants));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task A_wrong_case_permission_claim_is_denied()
  {
    var context = PlatformContext(Administer,
      new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new Claim(JwtClaimTypes.Permission, Administer.ToUpperInvariant()));

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  // ---- Cross-plane: platform handler never satisfies tenant requirements ----

  [Fact]
  public async Task The_platform_handler_ignores_a_tenant_permission_requirement()
  {
    // AuthorizationHandler<PlatformPermissionRequirement> only runs for its own requirement type.
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("test.permission")],
      Authenticated(
        new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
        new Claim(JwtClaimTypes.Permission, "test.permission")),
      resource: null);

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task The_platform_handler_ignores_a_tenant_role_requirement()
  {
    var context = new AuthorizationHandlerContext(
      [new RoleRequirement("test.role")],
      Authenticated(
        new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
        new Claim(JwtClaimTypes.Role, "test.role")),
      resource: null);

    await Handler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  // ---- Policy provider ----

  [Fact]
  public async Task Policy_provider_creates_a_platform_permission_requirement_for_a_valid_name()
  {
    var policy = await CreatePolicyProvider().GetPolicyAsync(
      PlatformPermissionAuthorizationDefaults.CreatePolicyName(Administer));

    Assert.NotNull(policy);
    Assert.Contains(policy.Requirements, requirement =>
      requirement is PlatformPermissionRequirement { Permission: Administer });
  }

  [Fact]
  public async Task Policy_provider_still_creates_the_tenant_requirements_and_does_not_collide()
  {
    var provider = CreatePolicyProvider();

    var tenantPermission = await provider.GetPolicyAsync(PermissionAuthorizationDefaults.CreatePolicyName("test.permission"));
    var tenantRole = await provider.GetPolicyAsync(RoleAuthorizationDefaults.CreatePolicyName("test.role"));
    var platform = await provider.GetPolicyAsync(PlatformPermissionAuthorizationDefaults.CreatePolicyName(Administer));

    Assert.Contains(tenantPermission!.Requirements, r => r is PermissionRequirement { Permission: "test.permission" });
    Assert.DoesNotContain(tenantPermission.Requirements, r => r is PlatformPermissionRequirement);
    Assert.Contains(tenantRole!.Requirements, r => r is RoleRequirement { Role: "test.role" });
    Assert.Contains(platform!.Requirements, r => r is PlatformPermissionRequirement { Permission: Administer });
    Assert.DoesNotContain(platform.Requirements, r => r is PermissionRequirement);
  }

  [Theory]
  [InlineData("PlatformPermission:")]
  [InlineData("PlatformPermission:bad..name")]
  [InlineData("PlatformPermission:9leading.digit")]
  [InlineData("platformpermission:Platform.Support.Administer")] // wrong-case prefix must not match
  public async Task Policy_provider_rejects_malformed_or_wrong_case_platform_policies(string policyName)
  {
    var policy = await CreatePolicyProvider().GetPolicyAsync(policyName);

    // A wrong-case prefix falls through to the default provider (no matching static policy) => null.
    Assert.Null(policy);
  }

  [Fact]
  public void Policy_name_helpers_use_the_platform_prefix()
  {
    Assert.Equal("PlatformPermission:", PlatformPermissionAuthorizationDefaults.PolicyPrefix);
    Assert.Equal($"PlatformPermission:{Administer}", PlatformPermissionAuthorizationDefaults.CreatePolicyName(Administer));
    Assert.True(PlatformPermissionAuthorizationDefaults.TryGetPermissionName($"PlatformPermission:{Administer}", out var parsed));
    Assert.Equal(Administer, parsed);
    Assert.False(PlatformPermissionAuthorizationDefaults.TryGetPermissionName($"Permission:{Administer}", out _));
  }

  // ---- Helpers ----

  private static PlatformPermissionAuthorizationHandler Handler() => new(new PlatformPermissionCatalog());

  private static PermissionAuthorizationPolicyProvider CreatePolicyProvider() => new(Options.Create(new AuthorizationOptions()));

  private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
    new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

  private static AuthorizationHandlerContext PlatformContext(string requiredPermission, params Claim[] claims) =>
    new([new PlatformPermissionRequirement(requiredPermission)], Authenticated(claims), resource: null);
}
