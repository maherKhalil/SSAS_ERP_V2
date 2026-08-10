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
  public void Only_the_platform_tenant_family_is_platform_support_scoped()
  {
    var catalog = new PlatformPermissionCatalog();

    var platformSupportPermissions = catalog.All
      .Where(permission => permission.Scope == PermissionScope.PlatformSupport)
      .ToArray();

    Assert.All(
      platformSupportPermissions,
      permission => Assert.StartsWith(PlatformTenantPrefix, permission.Name.Value, StringComparison.Ordinal));
  }

  [Fact]
  public void Every_non_platform_tenant_permission_stays_tenant_scoped()
  {
    // The tenant plane (Company, Localization, IAM) is unchanged: everything outside the
    // Platform.Tenants.* family remains PermissionScope.Tenant.
    var catalog = new PlatformPermissionCatalog();

    Assert.All(
      catalog.All.Where(permission => !permission.Name.Value.StartsWith(PlatformTenantPrefix, StringComparison.Ordinal)),
      permission => Assert.Equal(PermissionScope.Tenant, permission.Scope));
  }

  [Fact]
  public void Tenant_token_claim_filter_removes_every_platform_support_permission()
  {
    // Claim issuance must scope-filter: no PlatformSupport catalog permission can survive into a tenant token.
    var catalog = new PlatformPermissionCatalog();
    var allNames = catalog.All.Select(permission => permission.Name.Value).ToArray();

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(allNames, catalog);

    Assert.All(
      filtered,
      name => Assert.False(name.StartsWith(PlatformTenantPrefix, StringComparison.Ordinal)));
    Assert.Equal(
      catalog.All.Count(permission => permission.Scope == PermissionScope.Tenant),
      filtered.Count);
  }
}
