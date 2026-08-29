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

  // ---- WHY THERE IS NO "CompensationUpdateRefused".
  //
  // There is no update path to refuse. `OD-PAY-0003` ruled dated history, so a change is a NEW record and
  // the API has no PUT for it (`api-contracts.md`). An error for an operation that cannot be expressed would
  // be dead code advertising a door that does not exist.
  //
  // ---- ⚠ AND THAT REASONING RETIRED TWO CODES OF ITS OWN (T-168).
  //
  // `NotFound` and `NoneInForce` were declared, mapped to 404, and returned by nothing.
  // **`GetCompensationCurrentAsync` answers the 404 directly** — it returns `PayrollApiErrorMapper.NotFound`
  // when the read is null and never touches a domain code. The behaviour is shipped and right; these two
  // were the door with no room behind it that the paragraph above describes.
  //
  // ⚠ **`NoneInForce` must not be revived by wiring it into the calculator.** `PayrollCalculator` treats a
  // null compensation as `0m` DELIBERATELY: an employee with one-off payments and no compensation record is
  // a supported state, and refusing it there re-breaks the omission that was repaired in T-107.
}
