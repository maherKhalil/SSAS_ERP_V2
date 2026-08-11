using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.PlatformSupport;

/// <summary>
/// Defense-in-depth filter for platform-support authority reads (ADR-015 / DEC-TEN-0018) — the
/// platform-plane counterpart of <see cref="TenantPermissionClaimFilter"/>. Scope is derived from the
/// code-owned <see cref="IPermissionCatalog"/>, never trusted from stored assignment data. Only names
/// the catalog recognises as <see cref="PermissionScope.PlatformSupport"/> confer authority; a
/// force-seeded or corrupt tenant-scoped (or unknown) permission name is dropped, so it can never be
/// read back as platform-support authority even though write-side validation should already prevent it.
/// This is intentionally stricter than the tenant filter: unknown names are excluded, because platform
/// authority must be an explicitly recognised platform-support permission.
/// </summary>
public static class PlatformSupportPermissionFilter
{
  public static IReadOnlyCollection<string> FilterToPlatformSupportScope(
    IEnumerable<string> permissionNames,
    IPermissionCatalog catalog)
  {
    ArgumentNullException.ThrowIfNull(permissionNames);
    ArgumentNullException.ThrowIfNull(catalog);

    return permissionNames.Where(name => IsPlatformSupportScoped(name, catalog)).ToArray();
  }

  private static bool IsPlatformSupportScoped(string name, IPermissionCatalog catalog) =>
    catalog.TryGet(name, out var definition) && definition.Scope == PermissionScope.PlatformSupport;
}
