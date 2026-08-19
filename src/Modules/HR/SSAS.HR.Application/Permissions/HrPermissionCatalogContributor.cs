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
    new(HrPermissionNames.TerminateEmployees, "Terminate employees")
  ];

  public IReadOnlyCollection<ModulePermissionDefinition> Permissions => Definitions;
}
