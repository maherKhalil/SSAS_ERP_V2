namespace SSAS.Platform.Application.Permissions;

public static class PlatformPermissionNames
{
  public const string ViewUsers = "Platform.Users.View";
  public const string CreateUsers = "Platform.Users.Create";
  public const string UpdateUsers = "Platform.Users.Update";
  public const string DeactivateUsers = "Platform.Users.Deactivate";
  public const string ReactivateUsers = "Platform.Users.Reactivate";
  public const string AssignUserRoles = "Platform.UserRoles.Assign";
  public const string RemoveUserRoles = "Platform.UserRoles.Remove";
  public const string ViewRoles = "Platform.Roles.View";
  public const string CreateRoles = "Platform.Roles.Create";
  public const string UpdateRoles = "Platform.Roles.Update";
  public const string RequestRoleRetirement = "Platform.Roles.RequestRetirement";
  public const string RetireRoles = "Platform.Roles.Retire";
  public const string AssignRolePermissions = "Platform.RolePermissions.Assign";
  public const string RemoveRolePermissions = "Platform.RolePermissions.Remove";
  public const string ViewPermissions = "Platform.Permissions.View";
  public const string ViewLocalization = "Platform.Localization.View";
  public const string ManageLocalization = "Platform.Localization.Manage";
  public const string ViewLocalizationHistory = "Platform.Localization.ViewHistory";
  public const string ViewCompanies = "Platform.Companies.View";
  public const string ManageCompanies = "Platform.Companies.Manage";
  public const string CompanyLifecycle = "Platform.Companies.Lifecycle";

  // Platform-plane (PermissionScope.PlatformSupport) tenant-administration permissions (ADR-015, DEC-TEN-0018).
  // These are never assignable to tenant roles; scope is what makes them platform-plane, not the "Platform." prefix.
  public const string ViewTenants = "Platform.Tenants.View";
  public const string ManageTenants = "Platform.Tenants.Manage";
  public const string TenantLifecycle = "Platform.Tenants.Lifecycle";

  // Platform-authority administration permission (ADR-016, DEC-TEN-0021): governs platform-support
  // principal registration, permission grant/revoke, and Disable/Re-enable. PermissionScope.PlatformSupport.
  public const string AdministerPlatformSupport = "Platform.Support.Administer";
}
