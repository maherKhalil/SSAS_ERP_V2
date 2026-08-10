using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Permissions;

public sealed class PlatformPermissionCatalog : IPermissionCatalog
{
  private static readonly Dictionary<string, PermissionDefinition> Definitions = new[]
    {
      Define(PlatformPermissionNames.ViewUsers, "View tenant users"),
      Define(PlatformPermissionNames.CreateUsers, "Create tenant memberships"),
      Define(PlatformPermissionNames.UpdateUsers, "Update tenant user profiles"),
      Define(PlatformPermissionNames.DeactivateUsers, "Deactivate tenant users"),
      Define(PlatformPermissionNames.ReactivateUsers, "Reactivate tenant users"),
      Define(PlatformPermissionNames.AssignUserRoles, "Assign tenant roles to users"),
      Define(PlatformPermissionNames.RemoveUserRoles, "Remove tenant roles from users"),
      Define(PlatformPermissionNames.ViewRoles, "View tenant roles"),
      Define(PlatformPermissionNames.CreateRoles, "Create tenant custom roles"),
      Define(PlatformPermissionNames.UpdateRoles, "Update tenant custom roles"),
      Define(PlatformPermissionNames.RequestRoleRetirement, "Request tenant role retirement"),
      Define(PlatformPermissionNames.RetireRoles, "Retire tenant roles"),
      Define(PlatformPermissionNames.AssignRolePermissions, "Assign permissions to tenant roles"),
      Define(PlatformPermissionNames.RemoveRolePermissions, "Remove permissions from tenant roles"),
      Define(PlatformPermissionNames.ViewPermissions, "View the permission catalog"),
      Define(PlatformPermissionNames.ViewLocalization, "View localization resources"),
      Define(PlatformPermissionNames.ManageLocalization, "Manage localization overrides"),
      Define(PlatformPermissionNames.ViewLocalizationHistory, "View localization history"),
      Define(PlatformPermissionNames.ViewCompanies, "View companies"),
      Define(PlatformPermissionNames.ManageCompanies, "Create and update companies"),
      Define(PlatformPermissionNames.CompanyLifecycle, "Change company lifecycle state"),
      Define(PlatformPermissionNames.ViewTenants, "View platform tenant lifecycle records", PermissionScope.PlatformSupport),
      Define(PlatformPermissionNames.ManageTenants, "Create platform tenants", PermissionScope.PlatformSupport),
      Define(PlatformPermissionNames.TenantLifecycle, "Change platform tenant lifecycle state", PermissionScope.PlatformSupport)
    }
    .ToDictionary(definition => definition.Name.Value, StringComparer.Ordinal);

  public IReadOnlyCollection<PermissionDefinition> All => Definitions.Values.ToArray();

  public bool TryGet(string name, out PermissionDefinition permission) => Definitions.TryGetValue(name, out permission!);

  // Backward-compatible overload: existing callers keep tenant scope unchanged.
  private static PermissionDefinition Define(string name, string description) =>
    Define(name, description, PermissionScope.Tenant);

  private static PermissionDefinition Define(string name, string description, PermissionScope scope)
  {
    var permissionName = PermissionName.Create(name);
    if (permissionName.IsFailure)
    {
      throw new InvalidOperationException($"The permission '{name}' is invalid.");
    }

    return new PermissionDefinition(permissionName.Value, scope, description);
  }
}
