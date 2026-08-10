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
}
