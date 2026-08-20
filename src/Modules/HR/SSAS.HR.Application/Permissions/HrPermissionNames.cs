namespace SSAS.HR.Application.Permissions;

// THE CODE-OWNED HR EMPLOYEE PERMISSION SET (FP-006 authorization-model, DEC-EMP-0030).
//
// `<Plane>.<Resource>.<Action>`, matching the established platform convention and satisfying the platform
// permission-name grammar of exactly three ASCII-identifier segments, so the names themselves need no
// framework change.
//
// ---- NAMING THEM IS NOT REGISTERING THEM (FP-006P).
//
// This file is the single source of the names; it is not a catalog. A role may only be granted a permission
// the composed IPermissionCatalog DEFINES, and until FP-006P these constants were defined nowhere the
// role-assignment path could see them: no role could hold one, and every Employee endpoint refused every
// caller. HrPermissionCatalogContributor turns these constants into definitions, and the Host registers it.
// Adding a constant here without adding it there produces a permission that authorizes nothing.
//
// ---- THESE ARE FUNCTIONAL AUTHORITY, AND NOTHING ELSE.
//
// Holding one says which OPERATION is permitted. It says nothing about which companies or branches are
// reachable, which remain the independent scope dimensions resolved by ITenantCompanyAccessResolver and
// ITenantBranchAccessResolver. Conversely `Platform.Tenant.Administer` widens those scopes and grants NONE
// of these: an administrator without ViewEmployees cannot read an employee (ADR-025 decision 8).
public static class HrPermissionNames
{
  public const string ViewEmployees = "HR.Employees.View";

  public const string CreateEmployees = "HR.Employees.Create";

  public const string UpdateEmployees = "HR.Employees.Update";

  // Separated from Update because a transfer moves a record across a security partition and is the one
  // operation permitted to change BranchId (BRULE-EMP-0015).
  public const string TransferEmployees = "HR.Employees.Transfer";

  // Separated because termination is terminal and is a sensitive operation under BR-PLT-0103.
  public const string TerminateEmployees = "HR.Employees.Terminate";

  // ---- DEPARTMENT (FP-007 Phase 2, REQ-HR-0100/0101/0102).
  //
  // FOUR, AND DELIBERATELY NOT MORE. There is no `HR.Departments.Delete` because deletion does not exist —
  // a permission authorizing nothing is worse than none — and no `HR.Departments.Manage` catch-all, because
  // a permission whose description cannot say what it lets someone DO is one nobody can grant responsibly.
  public const string ViewDepartments = "HR.Departments.View";

  public const string CreateDepartments = "HR.Departments.Create";

  // Covers the ordinary edit, the hierarchy move, and manager assignment. Grouping the hierarchy move here
  // is a decision rather than an omission: a role able to rename a department but not move it is a
  // distinction no requirement asks for.
  public const string UpdateDepartments = "HR.Departments.Update";

  // Separated from Update because it changes whether a department can receive employees — materially
  // different authority from editing its label.
  public const string DeactivateDepartments = "HR.Departments.Deactivate";
}
