using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Compensation;

public static class OneOffPaymentErrors
{
  public static readonly Error CompanyRequired = new(
    "Payroll.OneOffPaymentCompanyRequired",
    "A one-off payment must belong to a company.",
    Field: "companyId");

  public static readonly Error EmployeeRequired = new(
    "Payroll.OneOffPaymentEmployeeRequired",
    "A one-off payment must name an employee.");

  public static readonly Error PeriodRequired = new(
    "Payroll.OneOffPaymentPeriodRequired",
    "A one-off payment must name the payroll period it is paid in.",
    Field: "payrollPeriodId");

  public static readonly Error PayElementRequired = new(
    "Payroll.OneOffPaymentPayElementRequired",
    "A one-off payment must name the pay element it is paid as, which supplies its kind and its account.",
    Field: "payElementId");

  // Zero is refused as well as negative — see `OneOffPayment.Create` for why a zero instruction is worse
  // than no instruction.
  public static readonly Error AmountNotPositive = new(
    "Payroll.OneOffPaymentAmountNotPositive",
    "A one-off payment must be a positive amount.",
    Field: "amount");

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

  // ---- ⚠ TWO CONSTANTS USED TO LIVE IN THIS FILE AND NEITHER SHOULD HAVE (T-125).
  //
  // **`PayElementNotFound` declared `Payroll.OneOffPaymentPayElementNotFound` for a condition that already
  // had a code.** `PayElementErrors.NotFound` is `Payroll.PayElementNotFound` and has been mapped to 404
  // since FP-012. **The same fact under two names is `DEC-L-080` in the error vocabulary** — and the second
  // name was unmapped, so *the pay element you named does not exist* answered **404 from one route and 500
  // from another.** `RecordOneOffPaymentCommandHandler` returns the existing code.
  //
  // **`NotFound` declared `Payroll.OneOffPaymentNotFound` and was returned by nothing at all** — a code for
  // a lookup path that does not exist, which reads to the next person as evidence that one does.

  // ---- A RUN FOR ANOTHER PERIOD REACHING THIS INSTRUCTION (T-124).
  //
  // **The instruction's period is part of its identity** (`OD-SS-0003`-era T-110: *"the instruction binds to
  // the PERIOD an operator names"*), and a run for a different period paying it would be a payment nobody
  // scheduled — in a period whose totals nobody expects it in.
  //
  // Unreachable through the current call path, whose query filters by period. **It exists because that
  // filter was the ONLY thing enforcing it**, and a rule with one enforcement point is one refactor from
  // having none.
  public static readonly Error ConsumingRunIsForAnotherPeriod = new(
    "Payroll.OneOffPaymentConsumingRunIsForAnotherPeriod",
    "A payroll run for a different period cannot pay this one-off payment.");

}
