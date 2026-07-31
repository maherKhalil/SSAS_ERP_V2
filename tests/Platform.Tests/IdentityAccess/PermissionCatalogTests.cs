using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.IdentityAccess;

public sealed class PermissionCatalogTests
{
  public static TheoryData<string, string> ExpectedPermissions => new()
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
    { "Platform.Permissions.View", "View the permission catalog" }
  };

  [Fact]
  public void Catalog_is_code_owned_unique_and_tenant_scoped()
  {
    var catalog = new PlatformPermissionCatalog();

    Assert.NotEmpty(catalog.All);
    Assert.Equal(catalog.All.Count, catalog.All.Select(item => item.Name.Value).Distinct(StringComparer.Ordinal).Count());
    Assert.All(catalog.All, item => Assert.Equal(PermissionScope.Tenant, item.Scope));
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
  [MemberData(nameof(ExpectedPermissions))]
  public void Catalog_contains_only_the_reviewed_milestone_permission(string identifier, string displayName)
  {
    var catalog = new PlatformPermissionCatalog();

    Assert.True(catalog.TryGet(identifier, out var permission));
    Assert.Equal(displayName, permission.Description);
    Assert.Equal(PermissionScope.Tenant, permission.Scope);
    Assert.Equal(3, identifier.Split('.').Length);
  }

  [Fact]
  public void Catalog_has_exactly_fifteen_permissions_and_no_deferred_operation()
  {
    var identifiers = new PlatformPermissionCatalog().All.Select(item => item.Name.Value).ToArray();

    Assert.Equal(15, identifiers.Length);
    Assert.DoesNotContain(identifiers, identifier =>
      identifier.Contains("Support", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Owner", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("Invitation", StringComparison.OrdinalIgnoreCase) ||
      identifier.Contains("TenantSelection", StringComparison.OrdinalIgnoreCase));
  }
}
