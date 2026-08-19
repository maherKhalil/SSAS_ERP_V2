using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.IdentityAccess;

public sealed class PermissionCatalogTests
{
  // Tenant-plane permissions (PermissionScope.Tenant): assignable to tenant roles.
  public static TheoryData<string, string> ExpectedTenantPermissions => new()
  {
    { "Platform.Users.View", "View tenant users" },
    { "Platform.Users.Create", "Create tenant memberships" },
    { "Platform.Users.Update", "Update tenant user profiles" },
    { "Platform.Users.Deactivate", "Deactivate tenant users" },
    { "Platform.Users.Reactivate", "Reactivate tenant users" },
    { "Platform.UserRoles.Assign", "Assign tenant roles to users" },
    { "Platform.UserRoles.Remove", "Remove tenant roles from users" },
    { "Platform.Roles.View", "View tenant roles" },
    { "Platform.Roles.Create", "Create tenant custom roles" },
    { "Platform.Roles.Update", "Update tenant custom roles" },
    { "Platform.Roles.RequestRetirement", "Request tenant role retirement" },
    { "Platform.Roles.Retire", "Retire tenant roles" },
    { "Platform.RolePermissions.Assign", "Assign permissions to tenant roles" },
    { "Platform.RolePermissions.Remove", "Remove permissions from tenant roles" },
    { "Platform.Permissions.View", "View the permission catalog" },
    { "Platform.Localization.View", "View localization resources" },
    { "Platform.Localization.Manage", "Manage localization overrides" },
    { "Platform.Localization.ViewHistory", "View localization history" },
    { "Platform.Companies.View", "View companies" },
    { "Platform.Companies.Manage", "Create and update companies" },
    { "Platform.Companies.Lifecycle", "Change company lifecycle state" }
  };

  // Platform-plane permissions (PermissionScope.PlatformSupport, ADR-015/016): never assignable to tenant roles.
  public static TheoryData<string, string> ExpectedPlatformSupportPermissions => new()
  {
    { "Platform.Tenants.View", "View platform tenant lifecycle records" },
    { "Platform.Tenants.Manage", "Create platform tenants" },
    { "Platform.Tenants.Lifecycle", "Change platform tenant lifecycle state" },
    { "Platform.Support.Administer", "Administer platform-support principals and their permission assignments" }
  };

  [Fact]
  public void Catalog_is_code_owned_and_unique()
  {
    var catalog = new PlatformPermissionCatalog();

    Assert.NotEmpty(catalog.All);
    Assert.Equal(catalog.All.Count, catalog.All.Select(item => item.Name.Value).Distinct(StringComparer.Ordinal).Count());
    Assert.All(catalog.All, item => Assert.True(item.Scope is PermissionScope.Tenant or PermissionScope.PlatformSupport));
  }

  [Fact]
  public void Catalog_lookup_uses_exact_ordinal_matching()
  {
    var catalog = new PlatformPermissionCatalog();

    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewUsers, out _));
    Assert.False(catalog.TryGet(PlatformPermissionNames.ViewUsers.ToUpperInvariant(), out _));
    Assert.False(catalog.TryGet($" {PlatformPermissionNames.ViewUsers}", out _));
  }

  [Theory]
  [MemberData(nameof(ExpectedTenantPermissions))]
  public void Catalog_tenant_permission_is_tenant_scoped(string identifier, string displayName)
  {
    var catalog = new PlatformPermissionCatalog();

    Assert.True(catalog.TryGet(identifier, out var permission));
    Assert.Equal(displayName, permission.Description);
    Assert.Equal(PermissionScope.Tenant, permission.Scope);
    Assert.Equal(3, identifier.Split('.').Length);
  }

  [Theory]
  [MemberData(nameof(ExpectedPlatformSupportPermissions))]
  public void Catalog_platform_tenant_permission_is_platform_support_scoped(string identifier, string displayName)
  {
    var catalog = new PlatformPermissionCatalog();

    Assert.True(catalog.TryGet(identifier, out var permission));
    Assert.Equal(displayName, permission.Description);
    Assert.Equal(PermissionScope.PlatformSupport, permission.Scope);
    Assert.Equal(3, identifier.Split('.').Length);
  }

  [Fact]
  public void Catalog_has_exactly_the_reviewed_permissions_split_by_scope()
  {
    var catalog = new PlatformPermissionCatalog();
    var identifiers = catalog.All.Select(item => item.Name.Value).ToArray();

    // 26 since Branch foundation B0/B1 added Platform.Tenant.Administer at TENANT scope: a tenant
    // administering itself, as opposed to the PlatformSupport-scoped family below, which is cross-tenant
    // authority and is never assignable to a tenant role. The count is asserted so a new permission cannot
    // enter the catalog without someone deciding which plane it belongs to.
    Assert.Equal(26, identifiers.Length);
    Assert.Equal(22, catalog.All.Count(item => item.Scope == PermissionScope.Tenant));
    Assert.Equal(4, catalog.All.Count(item => item.Scope == PermissionScope.PlatformSupport));

    // The platform-plane (PlatformSupport) family is exactly the tenant-admin permissions plus the
    // authority-administration permission; scope — not the "Platform." prefix — is authoritative.
    Assert.Equal(
      ["Platform.Support.Administer", "Platform.Tenants.Lifecycle", "Platform.Tenants.Manage", "Platform.Tenants.View"],
      catalog.All.Where(item => item.Scope == PermissionScope.PlatformSupport)
        .Select(item => item.Name.Value)
        .OrderBy(value => value, StringComparer.Ordinal));

    Assert.DoesNotContain(identifiers, identifier =>
      identifier.Contains("Owner", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Invitation", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("TenantSelection", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void Backward_compatible_two_arg_define_keeps_existing_permissions_tenant_scoped()
  {
    // Every permission outside the PlatformSupport-scoped family uses the original two-argument Define,
    // which must continue to default to PermissionScope.Tenant so no existing permission changes plane.
    var catalog = new PlatformPermissionCatalog();

    Assert.All(
      catalog.All.Where(item => item.Scope != PermissionScope.PlatformSupport),
      item => Assert.Equal(PermissionScope.Tenant, item.Scope));
  }

  [Fact]
  public void Existing_company_localization_and_iam_permissions_remain_tenant_scoped()
  {
    var catalog = new PlatformPermissionCatalog();

    foreach (var name in new[]
      {
        PlatformPermissionNames.ViewCompanies,
        PlatformPermissionNames.ManageCompanies,
        PlatformPermissionNames.CompanyLifecycle,
        PlatformPermissionNames.ViewLocalization,
        PlatformPermissionNames.ManageLocalization,
        PlatformPermissionNames.ViewLocalizationHistory,
        PlatformPermissionNames.ViewRoles,
        PlatformPermissionNames.AssignRolePermissions,
        PlatformPermissionNames.ViewUsers
      })
    {
      Assert.True(catalog.TryGet(name, out var permission));
      Assert.Equal(PermissionScope.Tenant, permission.Scope);
    }
  }
}
