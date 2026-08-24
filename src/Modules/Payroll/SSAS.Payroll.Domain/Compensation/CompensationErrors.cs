using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Compensation;

public static class CompensationErrors
{
  public static readonly Error CompanyRequired = new(
    "Payroll.CompensationCompanyRequired",
    "Compensation must belong to a company.");

  public static readonly Error EmployeeRequired = new(
    "Payroll.CompensationEmployeeRequired",
    "Compensation must name an employee.");

  public static readonly Error NegativeBaseAmount = new(
    "Payroll.CompensationBaseAmountNegative",
    "A base compensation amount cannot be negative.");

  public static readonly Error AssignmentElementRequired = new(
    "Payroll.CompensationAssignmentElementRequired",
    "A pay element assignment must name a pay element.");

  public static readonly Error NegativeAssignmentAmount = new(
    "Payroll.CompensationAssignmentAmountNegative",
    "A pay element assignment amount or rate cannot be negative.");

  // A duplicate would double-count the element in every run, silently — which is the worst kind of payroll
  // defect because the total still looks like a number.
  public static readonly Error DuplicateAssignment = new(
    "Payroll.CompensationAssignmentDuplicate",
    "The same pay element cannot be assigned twice in one compensation record.");

  public static readonly Error NotFound = new(
    "Payroll.CompensationNotFound",
    "No compensation record exists for this employee.");

  // ---- WHY THERE IS NO "CompensationUpdateRefused".
  //
  // There is no update path to refuse. `OD-PAY-0003` ruled dated history, so a change is a NEW record and
  // the API has no PUT for it (`api-contracts.md`). An error for an operation that cannot be expressed would
  // be dead code advertising a door that does not exist.
  public static Error NoneInForce(DateTimeOffset onUtc) => new(
    "Payroll.CompensationNoneInForce",
    $"No compensation was in force for this employee on {onUtc:yyyy-MM-dd}.");
}
