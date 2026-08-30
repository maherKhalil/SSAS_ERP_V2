using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Departments;

// NOTHING HERE NAMES DATABASE TOPOLOGY OR ANOTHER TENANT'S DATA (ADR-023, ADR-025).
//
// Scope refusals — a company the caller may not reach — are answered by the Platform boundaries with their
// own generic errors and are never restated here, so the HR surface cannot be used to probe for the
// existence of identifiers it is not allowed to see. This mirrors `EmployeeErrors` exactly.
//
// ---- THE FILE IS IN TWO HALVES, AND THE SPLIT IS MEANINGFUL.
//
// Above the Phase 2 banner are the refusals the AGGREGATE can decide alone — invalid code, invalid name,
// self-parent, a lifecycle transition that does not exist. Below it are the ones that need a repository
// lookup or application orchestration to reach, which is why Phase 1 deliberately carried none of them:
// an error constant for enforcement that does not exist yet advertises a guarantee nothing provides.
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

  // ================================================================================================
  // FP-007 PHASE 2 — THE REFUSALS THAT NEED A REPOSITORY TO REACH
  // ================================================================================================
  //
  // Phase 1 deliberately carried none of these, because the aggregate cannot decide any of them alone.
  // They arrive now with the application orchestration that can.

  public static readonly Error ParentNotFound =
    new("Department.ParentNotFound", "The parent department was not found.");

  // NOT "in a different company", stated generically on purpose. Naming the company would confirm that a
  // department exists somewhere the caller cannot see, which is the disclosure BR-PLT-0002 forbids.
  public static readonly Error ParentInDifferentCompany =
    new("Department.ParentInDifferentCompany", "The parent department belongs to a different company.");

  public static readonly Error ParentInactive =
    new("Department.ParentInactive", "The parent department is not active.");

  // BR-HR-0008. The general case: the proposed parent is somewhere beneath the department being moved.
  public static readonly Error HierarchyCycle =
    new("Department.HierarchyCycle", "The move would place a department beneath one of its own descendants.");

  // The hierarchy serialization lock could not be taken. A DISTINCT error rather than a generic failure,
  // because it is the one refusal here that is transient and worth retrying.
  public static readonly Error HierarchyMutationBusy =
    new("Department.HierarchyMutationBusy",
      "Another department hierarchy change is in progress for this company. Try again.");

  public static readonly Error HasActiveChildren =
    new("Department.HasActiveChildren",
      "The department cannot be deactivated while it has active child departments.");

  public static readonly Error ManagerEmployeeNotFound =
    new("Department.ManagerEmployeeNotFound", "The employee was not found.");

  public static readonly Error ManagerInDifferentCompany =
    new("Department.ManagerInDifferentCompany", "The employee belongs to a different company.");

  public static readonly Error ManagerTerminated =
    new("Department.ManagerTerminated", "A terminated employee cannot manage a department.");

  public static readonly Error ManagerNotAssigned =
    new("Department.ManagerNotAssigned", "The department has no manager to clear.");

  // The caller holds no company scope, or none covering this department. Deliberately the same shape as the
  // employee surface's refusal, so HR cannot be used to probe for companies.
  public static readonly Error CompanyScopeDenied =
    new("Department.CompanyScopeDenied", "The company is outside the caller's authorized scope.");

  // ⚠ TWO CODES, BECAUSE ONE CANNOT SAY WHICH PARAMETER TO FIX (T-260).
  //
  // The code these replaced covered three conditions -- page below one, page size below one,
  // page size above the maximum -- and all three answered the same 400 `request.invalid`. **A paging
  // client that fixes the wrong parameter retries and fails identically**, which is the same argument
  // that made a malformed identifier a 400 rather than a 404: a caller who cannot tell two conditions
  // apart cannot act on either.
  //
  // TWO rather than three: whether a page size was below one or above the maximum is visible to the
  // client from its own request. **And there is nowhere to say which bound** -- the problem document
  // carries `code`, `correlationId` and `resourceKey`, and no message field, so the code is the whole
  // channel.
  public static readonly Error InvalidPageNumber =
    new("Department.InvalidPageNumber", "The requested page number is out of range.");

  public static readonly Error InvalidPageSize =
    new("Department.InvalidPageSize", "The requested page size is out of range.");

  public static readonly Error PermissionDenied =
    new("Department.PermissionDenied", "The caller lacks the required department permission.");

  // Optimistic concurrency. The row moved under the caller between read and write.
  public static readonly Error ConcurrencyConflict =
    new("Department.ConcurrencyConflict", "The department was modified by another operation.");
}
