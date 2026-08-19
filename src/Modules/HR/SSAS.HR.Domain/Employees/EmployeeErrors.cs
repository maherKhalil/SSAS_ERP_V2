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
}
