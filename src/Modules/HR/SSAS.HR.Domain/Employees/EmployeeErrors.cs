using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Employees;

// NOTHING HERE NAMES DATABASE TOPOLOGY OR ANOTHER TENANT'S DATA (FP-006, ADR-023, ADR-025).
//
// Scope refusals — a company or branch the caller may not reach — are answered by the Platform boundaries
// with their own generic errors and never restated here, so the HR surface cannot be used to probe for the
// existence of identifiers it is not allowed to see.
public static class EmployeeErrors
{
  public static readonly Error InvalidEmployeeNumber =
    new("Employee.InvalidEmployeeNumber", "The employee number is invalid.");

  public static readonly Error InvalidNationalId =
    new("Employee.InvalidNationalId", "The national identifier is invalid.");

  public static readonly Error InvalidFullName =
    new("Employee.InvalidFullName", "The employee name is invalid.");

  public static readonly Error InvalidActor =
    new("Employee.InvalidActor", "A trusted lifecycle actor is required.");

  public static readonly Error InvalidEmploymentDate =
    new("Employee.InvalidEmploymentDate", "The employment date is invalid.");

  // BR-HR-0003. Employment Date cannot be later than Termination Date.
  public static readonly Error TerminationBeforeEmployment =
    new("Employee.TerminationBeforeEmployment", "The termination date cannot precede the employment date.");

  public static readonly Error InvalidTransition =
    new("Employee.InvalidTransition", "The employee lifecycle transition is invalid.");

  public static readonly Error InvalidTransitionReason =
    new("Employee.InvalidTransitionReason", "The lifecycle reason is invalid for this transition.");

  public static readonly Error NotFound = new("Employee.NotFound", "The employee was not found.");

  public static readonly Error NumberConflict =
    new("Employee.NumberConflict", "The employee number already exists within the company.");

  public static readonly Error NationalIdConflict =
    new("Employee.NationalIdConflict", "The national identifier already exists within the company.");

  // ---- TRANSFER (REQ-HR-0004, ADR-024).
  //
  // A terminated employee has no current branch assignment to move: employment ended, and relocating the
  // record afterwards would rewrite where they worked rather than where they now work.
  public static readonly Error TransferAfterTermination =
    new("Employee.TransferAfterTermination", "A terminated employee cannot be transferred.");

  // A transfer to the branch the employee is already in is not a transfer.
  public static readonly Error TransferDestinationUnchanged =
    new("Employee.TransferDestinationUnchanged", "The transfer destination is the employee's current branch.");

  public static readonly Error InvalidTransferReason =
    new("Employee.InvalidTransferReason", "The transfer reason is invalid.");

  // The append-only branch history is never edited. A correction is another transfer, not a rewrite.
  public static readonly Error BranchHistoryImmutable =
    new("Employee.BranchHistoryImmutable", "Employee branch history is append-only and cannot be modified.");

  public static readonly Error ConcurrencyConflict =
    new("Employee.ConcurrencyConflict", "The employee was modified concurrently; reload and retry.");

  // ---- READ SCOPE (FP-006C4, ADR-023 decision 22, ADR-025 decision 10).
  //
  // THE FUNCTIONAL DIMENSION IS DISTINGUISHABLE FROM THE SCOPE DIMENSIONS, deliberately. "You may not
  // perform this operation" discloses nothing about which companies or branches exist, so telling a caller
  // they lack the permission is safe and far more useful than a generic refusal.
  public static readonly Error ReadPermissionDenied =
    new("Employee.ReadPermissionDenied", "The HR employee view permission is required.");

  // ---- ONE ANSWER PER DIMENSION, GENERIC WITHIN IT.
  //
  // Unauthorized, inactive and nonexistent are INDISTINGUISHABLE (api-contracts). Whatever the resolver
  // underneath distinguishes, the HR surface collapses it, so a read cannot be used to probe which companies
  // or branches exist. An empty authorized set answers the same way — it never degrades to unfiltered.
  //
  // The two dimensions stay separable FROM EACH OTHER because a caller has to be able to tell whether to
  // select a different company or a different branch, and neither answer reveals anything about the other.
  public static readonly Error CompanyScopeDenied =
    new("Employee.CompanyScopeDenied", "The requested company scope is not available to this user.");

  public static readonly Error BranchScopeDenied =
    new("Employee.BranchScopeDenied", "The requested branch scope is not available to this user.");

  // A malformed scope REQUEST: branch identifiers supplied for a mode that takes none, an empty selection,
  // or an empty identifier. Safe to distinguish because it describes the REQUEST rather than any branch's
  // existence or state.
  public static readonly Error InvalidReadScope =
    new("Employee.InvalidReadScope", "The requested employee scope is not valid.");

  public static readonly Error InvalidPagination =
    new("Employee.InvalidPagination", "The requested page number or page size is out of range.");

  // ---- DEPARTMENT (FP-007 Phase 3, REQ-HR-0102).
  //
  // The functional authority to CHANGE an employee. A department change is an ordinary update rather than a
  // transfer: it moves nobody across a security partition, so it reuses `HR.Employees.Update` rather than
  // inventing a permission of its own (DEC-DEP-0018).
  public static readonly Error WritePermissionDenied =
    new("Employee.WritePermissionDenied", "The HR employee update permission is required.");

  // ---- ONE ANSWER FOR A DEPARTMENT THAT CANNOT BE USED, WHATEVER THE REASON.
  //
  // Nonexistent, in another tenant and in another company all answer identically. Distinguishing them would
  // turn employee creation into a probe for which departments exist outside the caller's company — the same
  // reasoning the read surface already applies to companies and branches above.
  //
  // INACTIVE IS DELIBERATELY SEPARATE. It describes a department the caller can already see and name, so it
  // reveals nothing new, and the operator needs to know that reactivating it is the fix rather than
  // hunting for a department that was never reachable.
  public static readonly Error DepartmentNotFound =
    new("Employee.DepartmentNotFound", "The department was not found.");

  public static readonly Error DepartmentInactive =
    new("Employee.DepartmentInactive", "The department is not active.");

  public static readonly Error DepartmentRequired =
    new("Employee.DepartmentRequired", "A department is required.");

  // The department-change counterpart of TransferDestinationUnchanged, and a failure for the same reason: a
  // request to move an employee where they already are is a malformed request, and answering it with
  // success would either append a history row describing no movement or return a success that did nothing.
  public static readonly Error DepartmentUnchanged =
    new("Employee.DepartmentUnchanged", "The destination is the employee's current department.");

  public static readonly Error DepartmentHistoryImmutable =
    new(
      "Employee.DepartmentHistoryImmutable",
      "Employee department history is append-only and cannot be modified.");
}
