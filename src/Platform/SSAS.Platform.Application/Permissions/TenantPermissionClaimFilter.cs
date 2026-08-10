using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Permissions;

/// <summary>
/// Defense-in-depth filter for tenant-token permission claims (ADR-015, DEC-TEN-0018, AC-TEN-0030).
/// Scope is derived from the code-owned <see cref="IPermissionCatalog"/>, never trusted from stored
/// assignment data. A permission the catalog knows to be non-<see cref="PermissionScope.Tenant"/>
/// (for example a force-seeded or corrupt <see cref="PermissionScope.PlatformSupport"/> assignment) is
/// dropped so it can never surface as a tenant-token claim, even though write-side validation
/// (<c>Role.AssignPermission</c>) should already prevent such an assignment.
/// Unknown permission names are preserved (existing behavior); an unknown name cannot match a known
/// platform permission requirement, so passing it through changes no security outcome.
/// </summary>
public static class TenantPermissionClaimFilter
{
  public static IReadOnlyCollection<string> FilterToTenantScope(
    IEnumerable<string> permissionNames,
    IPermissionCatalog catalog)
  {
    ArgumentNullException.ThrowIfNull(permissionNames);
    ArgumentNullException.ThrowIfNull(catalog);

    return permissionNames.Where(name => IsTenantScoped(name, catalog)).ToArray();
  }

  private static bool IsTenantScoped(string name, IPermissionCatalog catalog) =>
    !catalog.TryGet(name, out var definition) || definition.Scope == PermissionScope.Tenant;
}
