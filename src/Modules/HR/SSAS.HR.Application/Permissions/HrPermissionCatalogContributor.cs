using SSAS.BuildingBlocks.Tenancy.Permissions;

namespace SSAS.HR.Application.Permissions;

// HR'S FIVE EMPLOYEE PERMISSIONS, OFFERED TO THE COMPOSED CATALOG (FP-006P, ADR-012 r1.2, DEC-EMP-0030).
//
// ---- ONE DEFINITION, DERIVED FROM THE ONE SOURCE.
//
// The names come from `HrPermissionNames` rather than being restated here, so the constant the endpoints
// and the scope resolver compare against IS the constant registered in the catalog. A second spelling
// would be a permission that authorizes nothing, and it would look correct in both places.
//
// ---- THE DESCRIPTIONS ARE PART OF THE CONTRACT.
//
// They are what a tenant administrator reads when deciding whether to grant one, so they say what the
// permission lets someone DO — not which endpoint it unlocks. `Update` names activate and deactivate
// explicitly because grouping them under it was a decision (DEC-EMP-0030) rather than an omission.
//
// ---- SCOPE IS NOT STATED HERE BECAUSE IT CANNOT BE.
//
// The contract carries no scope: the composer stamps `PermissionScope.Tenant`. These are functional
// authority and say nothing about which companies or branches are reachable, which remain independent
// runtime dimensions resolved per operation (ADR-025 decision 8).
public sealed class HrPermissionCatalogContributor : IPermissionCatalogContributor
{
  private static readonly ModulePermissionDefinition[] Definitions =
  [
    new(HrPermissionNames.ViewEmployees,
      "View employees within the caller's authorized company and branch scope"),
    new(HrPermissionNames.CreateEmployees, "Create employees"),
    new(HrPermissionNames.UpdateEmployees, "Update employee profiles, and activate or deactivate employees"),
    new(HrPermissionNames.TransferEmployees, "Transfer employees between branches"),
    new(HrPermissionNames.TerminateEmployees, "Terminate employees"),
    new(HrPermissionNames.ImportEmployees, "Create employees in bulk from a file"),
    // The description says what leaves rather than what the operation is called, because this is the one
    // permission whose grant an administrator should think twice about.
    new(HrPermissionNames.ExportEmployees, "Extract employee records to a file that leaves the system"),

    // ---- DEPARTMENT (FP-007 Phase 2).
    //
    // `Update` names the hierarchy move and manager assignment explicitly, because grouping them under it
    // was a decision rather than an oversight — the same reason `Update` names activate and deactivate for
    // employees. An administrator granting this should know it moves org structure, not just labels.
    new(HrPermissionNames.ViewDepartments,
      "View departments and the department hierarchy within the caller's authorized company scope"),
    new(HrPermissionNames.CreateDepartments, "Create departments"),
    new(HrPermissionNames.UpdateDepartments,
      "Update department code and name, move departments within the hierarchy, and assign or clear " +
      "department managers"),
    new(HrPermissionNames.DeactivateDepartments, "Deactivate and reactivate departments"),

    // ---- POSITION AND THE TWO GRADE LADDERS (FP-008 Phase 2, DEC-POS-0018).
    //
    // TWELVE, taking the HR plane to twenty-one. `Update` names the re-grade explicitly for the same reason
    // the department `Update` names the hierarchy move: the grouping was a decision, and an administrator
    // granting it should know it moves org structure rather than just labels.
    new(HrPermissionNames.ViewPositions,
      "View positions within the caller's authorized company scope"),
    new(HrPermissionNames.CreatePositions, "Create positions"),
    new(HrPermissionNames.UpdatePositions,
      "Update a position's title and code, and change the job grade it is assigned to"),
    new(HrPermissionNames.DeactivatePositions, "Deactivate and reactivate positions"),

    new(HrPermissionNames.ViewJobGrades,
      "View job grades within the caller's authorized company scope"),
    new(HrPermissionNames.CreateJobGrades, "Create job grades"),
    new(HrPermissionNames.UpdateJobGrades,
      "Update a job grade's code, name and rank order, and change the salary grade it maps to"),
    new(HrPermissionNames.DeactivateJobGrades, "Deactivate and reactivate job grades"),

    // ---- THE SENSITIVE ONE, AND ITS DESCRIPTION SAYS SO.
    //
    // A tenant administrator deciding whether to grant this is deciding whether the holder may read the pay
    // structure. Describing it as "view salary grades" would make it read like any other catalog permission
    // and hide the disclosure the separation exists to make deliberate (`DEC-EMP-0030` precedent).
    new(HrPermissionNames.ViewSalaryGrades,
      "View salary grades INCLUDING their pay bands, within the caller's authorized company scope. " +
      "Granting this discloses the pay structure and is separate from viewing positions for that reason"),
    new(HrPermissionNames.CreateSalaryGrades, "Create salary grades"),
    new(HrPermissionNames.UpdateSalaryGrades,
      "Update a salary grade's code, name and rank order, and set or withdraw its minimum, midpoint and " +
      "maximum amounts"),
    new(HrPermissionNames.DeactivateSalaryGrades, "Deactivate and reactivate salary grades")
  ];

  public IReadOnlyCollection<ModulePermissionDefinition> Permissions => Definitions;
}
