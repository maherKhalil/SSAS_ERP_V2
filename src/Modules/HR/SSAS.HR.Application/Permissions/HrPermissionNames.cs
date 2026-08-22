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

  // ================================================================================================
  // POSITION AND THE TWO GRADE LADDERS (FP-008 Phase 2, DEC-POS-0018)
  // ================================================================================================
  //
  // TWELVE, in three families of four. `OD-POS-002` ruled three aggregates, and each is a resource a role
  // can be granted authority over independently.
  //
  // No `Delete` in any family — deletion does not exist (`BRULE-POS-0012`), so the permission would
  // authorize nothing. No `Manage` catch-all in any family — a permission whose description cannot say what
  // it lets someone DO is one nobody can grant responsibly. Both carried unchanged from `DEC-DEP-0017`.
  public const string ViewPositions = "HR.Positions.View";

  public const string CreatePositions = "HR.Positions.Create";

  // Covers the retitle, the recode, AND the change of grade reference. Grouping the re-grade here is a
  // decision rather than an omission: a role able to retitle a position but not re-grade it is a distinction
  // no requirement asks for — the same shape as the department hierarchy move under `UpdateDepartments`.
  public const string UpdatePositions = "HR.Positions.Update";

  // Separated from Update because it changes whether a position can receive employees (`BRULE-POS-0013`) —
  // materially different authority from editing its label. BOTH DIRECTIONS, following `DEC-DEP-0025`:
  // granting reactivation under ordinary Update would let a caller who may only retitle undo a closure
  // someone holding the sensitive permission deliberately made.
  public const string DeactivatePositions = "HR.Positions.Deactivate";

  public const string ViewJobGrades = "HR.JobGrades.View";

  public const string CreateJobGrades = "HR.JobGrades.Create";

  public const string UpdateJobGrades = "HR.JobGrades.Update";

  public const string DeactivateJobGrades = "HR.JobGrades.Deactivate";

  // ---- THE ONE DEPARTURE FROM THE MINIMAL SET, AND IT IS DELIBERATE.
  //
  // `HR.SalaryGrades.View` is NOT merged into `HR.Positions.View`. Pay bands are more sensitive than job
  // titles, and a single org-structure `View` would mean everyone who may read the organization chart may
  // also read the pay structure — a disclosure decision taken by accident rather than on purpose.
  //
  // This codebase already treats SENSITIVITY, not resource identity, as sufficient grounds for a separate
  // permission: `DEC-EMP-0030` separated `HR.Employees.Terminate` and `HR.Employees.Transfer` from `Update`
  // on exactly that basis. The architect ratified the separation explicitly (`DEC-POS-0018`), and it is
  // recorded as a departure because the FP-007 discipline is "four, and deliberately not more".
  //
  // The separation is enforced by the type system rather than by convention: only this permission produces a
  // `SalaryGradeReadScope`, and only that scope reaches the amounts.
  public const string ViewSalaryGrades = "HR.SalaryGrades.View";

  public const string CreateSalaryGrades = "HR.SalaryGrades.Create";

  // Covers `FR-POS-0209` — setting the informational minimum, midpoint and maximum — as well as the code,
  // name and rank. The amounts are not separated out: they are the salary grade's substance, and a role able
  // to rename a pay band but not price it is a distinction no requirement asks for.
  public const string UpdateSalaryGrades = "HR.SalaryGrades.Update";

  public const string DeactivateSalaryGrades = "HR.SalaryGrades.Deactivate";
}
