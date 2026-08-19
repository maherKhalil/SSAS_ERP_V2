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
}
