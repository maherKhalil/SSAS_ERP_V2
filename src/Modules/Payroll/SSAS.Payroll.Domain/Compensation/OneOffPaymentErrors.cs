using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Compensation;

public static class OneOffPaymentErrors
{
  public static readonly Error CompanyRequired = new(
    "Payroll.OneOffPaymentCompanyRequired",
    "A one-off payment must belong to a company.");

  public static readonly Error EmployeeRequired = new(
    "Payroll.OneOffPaymentEmployeeRequired",
    "A one-off payment must name an employee.");

  public static readonly Error PeriodRequired = new(
    "Payroll.OneOffPaymentPeriodRequired",
    "A one-off payment must name the payroll period it is paid in.");

  public static readonly Error PayElementRequired = new(
    "Payroll.OneOffPaymentPayElementRequired",
    "A one-off payment must name the pay element it is paid as, which supplies its kind and its account.");

  // Zero is refused as well as negative — see `OneOffPayment.Create` for why a zero instruction is worse
  // than no instruction.
  public static readonly Error AmountNotPositive = new(
    "Payroll.OneOffPaymentAmountNotPositive",
    "A one-off payment must be a positive amount.");

  public static readonly Error ConsumingRunRequired = new(
    "Payroll.OneOffPaymentConsumingRunRequired",
    "Consuming a one-off payment must name the run that paid it.");

  // ---- THE SAME RUN CONSUMING IT TWICE (T-123, narrowed from T-110).
  //
  // **Not "already paid by some run"** — a correcting run legitimately takes over an instruction whose
  // first run was reversed, which is the T-110 predicate `ReversedUtc` finally made expressible. **This is
  // one run repeating itself, which is a defect in the approval path rather than a correction.**
  public static readonly Error AlreadyConsumed = new(
    "Payroll.OneOffPaymentAlreadyConsumed",
    "This one-off payment has already been paid by this run.");

  // ---- THE ELEMENT MUST EXIST AND BE THE COMPANY'S.
  //
  // A one-off naming an element that does not exist has no kind and no GL account, so it could not produce
  // a line at all. Refused when it is written rather than discovered when a run is calculated.
  public static readonly Error PayElementNotFound = new(
    "Payroll.OneOffPaymentPayElementNotFound",
    "The pay element this one-off payment names does not exist for this company.");

  public static readonly Error NotFound = new(
    "Payroll.OneOffPaymentNotFound",
    "The one-off payment was not found.");
}
