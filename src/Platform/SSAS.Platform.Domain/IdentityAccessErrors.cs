using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain;

public static class IdentityAccessErrors
{
  public static readonly Error InvalidIdentity = new("Identity.Invalid", "Identity values are invalid.");
  public static readonly Error InvalidEmail = new("TenantUser.InvalidEmail", "The email address is invalid.");
  public static readonly Error InvalidDisplayName = new("TenantUser.InvalidDisplayName", "The display name is invalid.");
  public static readonly Error InvalidRoleName = new("Role.InvalidName", "The role name is invalid.");
  public static readonly Error InvalidPermission = new("Permission.Invalid", "The permission identifier is invalid.");
  public static readonly Error TenantRequired = new("Tenant.Required", "A trusted tenant is required.");
  public static readonly Error TenantMismatch = new("Tenant.Mismatch", "The related records must belong to the same tenant.");
  public static readonly Error DuplicateRoleAssignment = new("TenantUser.RoleAlreadyAssigned", "The role is already assigned.");
  public static readonly Error RoleAssignmentNotFound = new("TenantUser.RoleAssignmentNotFound", "The active role assignment was not found.");
  public static readonly Error InactiveTenantUser = new("TenantUser.Inactive", "The tenant user must be active.");
  public static readonly Error InvalidTenantUserTransition = new("TenantUser.InvalidTransition", "The tenant user status transition is invalid.");
  public static readonly Error DuplicatePermissionAssignment = new("Role.PermissionAlreadyAssigned", "The permission is already assigned.");
  public static readonly Error PermissionAssignmentNotFound = new("Role.PermissionAssignmentNotFound", "The active permission assignment was not found.");
  public static readonly Error PlatformPermissionRejected = new("Role.PlatformPermissionRejected", "Tenant roles cannot receive platform-support permissions.");
  public static readonly Error RoleNotAssignable = new("Role.NotAssignable", "The role cannot receive new user assignments.");
  public static readonly Error ProtectedSystemRole = new("Role.ProtectedSystemRole", "The protected system role cannot be changed by tenant administration.");
  public static readonly Error InvalidRoleTransition = new("Role.InvalidTransition", "The role status transition is invalid.");
  public static readonly Error RoleHasActiveUsers = new("Role.HasActiveUsers", "The role cannot be retired while assigned to an active user.");
  // The identity-to-employee mapping's only construction guard (ADR-030, FP-015). It refuses a link that
  // names nothing on one of its two sides; the CARDINALITY rule is enforced by two unique indexes rather
  // than here, because a rule that relies on every write path remembering is one nothing catches.
  public static readonly Error InvalidUserEmployeeLink = new("UserEmployeeLink.Invalid", "A user-employee link requires a tenant, a tenant user and an employee.");

  public static readonly Error ConcurrencyConflict = new("Persistence.ConcurrencyConflict", "The record was changed by another operation.");
  public static readonly Error UniqueConstraintViolation = new("Persistence.UniqueConstraint", "The requested identity or access value already exists.");
  public static readonly Error WriteFailure = new("Persistence.WriteFailure", "The identity or access change could not be persisted.");
  public static readonly Error NotFound = new("Common.NotFound", "The requested record was not found.");
  public static readonly Error Unauthorized = new("Authorization.Unauthorized", "An authenticated actor and trusted tenant are required.");
}
