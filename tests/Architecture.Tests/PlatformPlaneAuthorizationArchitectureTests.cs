using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Architecture.Tests;

// Durable platform-plane authorization invariants (ADR-015, DEC-TEN-0018).
// Behavior rules over the code-owned permission catalog and the tenant-token claim filter.
public sealed class PlatformPlaneAuthorizationArchitectureTests
{
  private const string PlatformTenantPrefix = "Platform.Tenants.";

  [Fact]
  public void Every_platform_tenant_permission_is_platform_support_scoped()
  {
    var catalog = new PlatformPermissionCatalog();

    var platformTenantPermissions = catalog.All
      .Where(permission => permission.Name.Value.StartsWith(PlatformTenantPrefix, StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(platformTenantPermissions);
    Assert.All(platformTenantPermissions, permission => Assert.Equal(PermissionScope.PlatformSupport, permission.Scope));
  }

  [Fact]
  public void The_platform_support_family_is_exactly_the_approved_permissions()
  {
    // PlatformSupport scope = the Platform.Tenants.* tenant-admin family (ADR-015) plus the
    // Platform.Support.Administer authority-administration permission (ADR-016). Nothing else.
    var catalog = new PlatformPermissionCatalog();

    Assert.Equal(
      ["Platform.Support.Administer", "Platform.Tenants.Lifecycle", "Platform.Tenants.Manage", "Platform.Tenants.View"],
      catalog.All.Where(permission => permission.Scope == PermissionScope.PlatformSupport)
        .Select(permission => permission.Name.Value)
        .OrderBy(value => value, StringComparer.Ordinal));
  }

  [Fact]
  public void Every_non_platform_support_permission_stays_tenant_scoped()
  {
    // The tenant plane (Company, Localization, IAM) is unchanged: everything outside the
    // PlatformSupport-scoped family remains PermissionScope.Tenant.
    var catalog = new PlatformPermissionCatalog();

    Assert.All(
      catalog.All.Where(permission => permission.Scope != PermissionScope.PlatformSupport),
      permission => Assert.Equal(PermissionScope.Tenant, permission.Scope));
  }

  [Fact]
  public void Tenant_token_claim_filter_removes_every_platform_support_permission()
  {
    // Claim issuance must scope-filter: no PlatformSupport catalog permission can survive into a tenant token.
    var catalog = new PlatformPermissionCatalog();
    var allNames = catalog.All.Select(permission => permission.Name.Value).ToArray();

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(allNames, catalog);

    // Every surviving name resolves to Tenant scope; no PlatformSupport permission (Tenants.* or Support.*) survives.
    Assert.All(
      filtered,
      name => Assert.True(catalog.TryGet(name, out var permission) && permission.Scope == PermissionScope.Tenant));
    Assert.Equal(
      catalog.All.Count(permission => permission.Scope == PermissionScope.Tenant),
      filtered.Count);
  }

  // ---- Phase 3C-1 platform token profile (ADR-015 / DEC-TEN-0022) ----

  [Fact]
  public void Security_plane_claim_name_and_platform_value_are_exact()
  {
    Assert.Equal("security_plane", JwtClaimTypes.SecurityPlane);
    Assert.Equal("platform", SecurityPlane.Platform);
    Assert.Equal("tenant", SecurityPlane.Tenant);
  }

  [Fact]
  public void Platform_access_token_claims_carry_no_tenant_or_role_shaped_fields()
  {
    // The platform profile is structurally non-tenant and permission-based (no roles/status/plane field).
    var properties = typeof(PlatformAccessTokenClaims).GetProperties().Select(property => property.Name).ToArray();

    foreach (var forbidden in new[] { "TenantId", "TenantUserId", "CompanyId", "Roles", "Role", "Status", "SecurityPlane" })
    {
      Assert.DoesNotContain(forbidden, properties);
    }
  }

  [Fact]
  public void Tenant_access_token_claims_remain_tenant_shaped()
  {
    var properties = typeof(AccessTokenClaims).GetProperties().Select(property => property.Name).ToArray();

    Assert.Contains("TenantId", properties);
    Assert.Contains("TenantUserId", properties);
    Assert.Contains("Roles", properties);
  }

  [Fact]
  public void Access_token_issuer_selects_the_plane_by_type_never_by_a_caller_flag()
  {
    // No Issue overload may take a bool or a raw string (a plane selector). The plane is chosen by the
    // strongly-typed claims argument (AccessTokenClaims vs PlatformAccessTokenClaims) only.
    var issueMethods = typeof(IAccessTokenIssuer).GetMethods().Where(method => method.Name == "Issue").ToArray();

    Assert.NotEmpty(issueMethods);
    foreach (var method in issueMethods)
    {
      var parameters = method.GetParameters();
      Assert.Equal(2, parameters.Length);
      Assert.Contains(parameters[0].ParameterType, new[] { typeof(AccessTokenClaims), typeof(PlatformAccessTokenClaims) });
      Assert.Equal(typeof(DateTimeOffset), parameters[1].ParameterType);
      Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(string));
    }
  }
}
