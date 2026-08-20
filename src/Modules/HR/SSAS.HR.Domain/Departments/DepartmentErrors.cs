using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Departments;

// NOTHING HERE NAMES DATABASE TOPOLOGY OR ANOTHER TENANT'S DATA (ADR-023, ADR-025).
//
// Scope refusals — a company the caller may not reach — are answered by the Platform boundaries with their
// own generic errors and are never restated here, so the HR surface cannot be used to probe for the
// existence of identifiers it is not allowed to see. This mirrors `EmployeeErrors` exactly.
//
// ---- WHAT IS DELIBERATELY ABSENT IN PHASE 1.
//
// There is no `ParentInDifferentCompany`, no `ParentInactive`, no `ParentIsDescendant` and no manager
// validation error. Each of those needs a repository lookup or application orchestration, and Phase 1
// implements only invariants the aggregate can decide alone. Adding the error constants early would
// advertise enforcement that does not exist yet.
public static class DepartmentErrors
{
  public static readonly Error InvalidCode =
    new("Department.InvalidCode", "The department code is invalid.");

  public static readonly Error InvalidName =
    new("Department.InvalidName", "The department name is invalid.");

  public static readonly Error InvalidActor =
    new("Department.InvalidActor", "A trusted lifecycle actor is required.");

  public static readonly Error NotFound = new("Department.NotFound", "The department was not found.");

  public static readonly Error CodeConflict =
    new("Department.CodeConflict", "The department code already exists within the company.");

  // ---- HIERARCHY (REQ-HR-0101, BR-HR-0008).
  //
  // The ONLY cycle case the aggregate can decide without I/O. The general "new parent is a descendant"
  // rule needs an ancestry walk and belongs to Phase 2; its error will join this file then.
  public static readonly Error ParentIsSelf =
    new("Department.ParentIsSelf", "A department cannot be its own parent.");

  // An empty identifier is not a department. Distinct from ParentIsSelf so the refusal says what was
  // actually wrong rather than borrowing a neighbouring rule's message.
  public static readonly Error InvalidParent =
    new("Department.InvalidParent", "The parent department reference is invalid.");

  // ---- LIFECYCLE.
  public static readonly Error InvalidTransition =
    new("Department.InvalidTransition", "The department lifecycle transition is invalid.");

  // ---- MANAGER (REQ-HR-0102).
  public static readonly Error InvalidManagerAssignment =
    new("Department.InvalidManagerAssignment", "The department manager assignment is invalid.");

  // ---- DEPARTMENT HISTORY.
  //
  // The append-only assignment log is never edited. A correction is another department change, never a
  // rewrite — exactly as for the branch history.
  public static readonly Error DepartmentHistoryImmutable =
    new("Department.DepartmentHistoryImmutable", "The employee department history cannot be modified.");

  public static readonly Error InvalidDepartmentAssignment =
    new("Department.InvalidDepartmentAssignment", "The employee department assignment is invalid.");
}
