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
}
