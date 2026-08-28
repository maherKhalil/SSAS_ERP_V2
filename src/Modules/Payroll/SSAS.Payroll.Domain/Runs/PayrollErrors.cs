using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Runs;

public static class PayrollErrors
{
  // The caller is a tenant user with no linked employee (`ADR-030` Decision 5, FP-015). **An ordinary state
  // rather than a fault** — platform-support staff, and users created before their employee record exists.
  // `ADR-030` puts it plainly: *"a support administrator opening a self-service page is not a fault
  // condition; it is Tuesday."*
  //
  // Mapped to `404 payroll.no_linked_employee` and deliberately NOT to `payroll.not_found`: that code says
  // *the thing you named does not exist*, and this one is about the CALLER. Telling an employee their
  // payslips were not found, when the truth is that nobody linked their record, points them at the wrong
  // remedy and the wrong person to ask.
  public static readonly Error NoLinkedEmployee = new(
    "Payroll.NoLinkedEmployee",
    "No employee record is linked to this user.");

  // ---- PERIODS.
  public static readonly Error PeriodCompanyRequired = new(
    "Payroll.PeriodCompanyRequired",
    "A payroll period must belong to a company.");

  public static readonly Error PeriodFiscalPeriodRequired = new(
    "Payroll.PeriodFiscalPeriodRequired",
    "A payroll period must be aligned to a fiscal period.");

  public static readonly Error PeriodNameInvalid = new(
    "Payroll.PeriodNameInvalid",
    "A payroll period name is required and must be at most 128 characters.");

  public static readonly Error PeriodBoundsInvalid = new(
    "Payroll.PeriodBoundsInvalid",
    "A payroll period must end after it starts.");

  public static readonly Error PayDateBeforePeriod = new(
    "Payroll.PayDateBeforePeriod",
    "A pay date cannot fall before the period it pays for.");

  public static readonly Error PeriodNotFound = new(
    "Payroll.PeriodNotFound",
    "The payroll period does not exist.");

  public static readonly Error PeriodAlreadyExists = new(
    "Payroll.PeriodConflict",
    "A payroll period already exists for this company and fiscal period.");

  // ---- RUNS.
  public static readonly Error RunCompanyRequired = new(
    "Payroll.RunCompanyRequired",
    "A payroll run must belong to a company.");

  public static readonly Error RunPeriodRequired = new(
    "Payroll.RunPeriodRequired",
    "A payroll run must name a payroll period.");

  public static readonly Error RunNotFound = new(
    "Payroll.RunNotFound",
    "The payroll run does not exist.");

  public static readonly Error RunHasNoLines = new(
    "Payroll.RunHasNoLines",
    "A payroll run with no calculated lines cannot be approved.");

  public static readonly Error RunJournalRequired = new(
    "Payroll.RunJournalRequired",
    "A posted payroll run must record the journal it produced.");

  public static readonly Error RunAlreadyExistsForPeriod = new(
    "Payroll.RunConflict",
    "A payroll run already exists for this company and period.");

  // ---- THE STATE REFUSALS NAME THE STATE THEY FOUND.
  //
  // `AccountErrors.Inactive` set this standard: naming the thing is the difference between a user fixing
  // something and a user filing a ticket. "A run cannot be approved" makes someone go and look; "a run in
  // Posted cannot be approved" already told them what happened.
  public static Error RunNotRecalculable(PayrollRunStatus status) => new(
    "Payroll.RunNotRecalculable",
    $"A payroll run in {status} cannot be recalculated; correct a posted run by reversing it and running again.");

  public static Error RunNotApprovable(PayrollRunStatus status) => new(
    "Payroll.RunNotApprovable",
    $"A payroll run in {status} cannot be approved; it must be Calculated first.");

  public static Error RunNotPostable(PayrollRunStatus status) => new(
    "Payroll.RunNotPostable",
    $"A payroll run in {status} cannot be posted; it must be Approved first.");

  // ---- THE LEDGER REFUSALS.
  //
  // These exist because `OD-PAY-0014` requires the closed-period refusal to NAME the period, which is only
  // possible because the posting contract answers with a closed set of outcomes rather than a problem-code
  // string (see `JournalPostingStatus`).
  public static Error PeriodClosedForPosting(string periodName) => new(
    "Payroll.FiscalPeriodClosed",
    $"Fiscal period '{periodName}' is closed, so this run cannot be approved.");

  public static readonly Error FiscalPeriodNotFound = new(
    "Payroll.FiscalPeriodNotFound",
    "No fiscal period covers this run's pay date, so it cannot be approved.");

  public static readonly Error LedgerRefusedPosting = new(
    "Payroll.LedgerRefusedPosting",
    "The ledger refused this posting, so the run has not been posted.");

  public static readonly Error LedgerRefusedReversal = new(
    "Payroll.LedgerRefusedReversal",
    "The ledger refused this reversal, so the run's journal is unchanged.");

  public static readonly Error RunNotReversible = new(
    "Payroll.RunNotReversible",
    "Only a posted payroll run can be reversed.");

  // ---- CALCULATION.
  // ---- FP-013, OD-ATT-0010. The attendance gate at approval.
  //
  // Narrow on purpose: only a period that EXISTS and is still OPEN is refused. A company that records no
  // attendance has no attendance period, and refusing that would make FP-013 a prerequisite for running
  // payroll at all.
  public static readonly Error AttendancePeriodOpen = new(
    "Payroll.AttendancePeriodOpen",
    "The attendance period covering this pay date is still open; close it before approving the run.");

  public static readonly Error NoIncludedEmployees = new(
    "Payroll.NoIncludedEmployees",
    "No employee was employed during this period, so there is nothing to calculate.");

  // ---- A DAILY SALARY WITH NO WORKING DAYS TO PRICE (T-108).
  //
  // `SalaryType.Daily` is `rate x standard working days - unpaid absent days`, and the standard working
  // days arrive on the attendance summary. Zero of them means there is nothing to multiply.
  //
  // **RENAMED FROM `DailySalaryHasNoWorkedDayCount` (T-107).** That name described a MISSING FIELD, and the
  // field now exists — keeping it would have left a constant named after a condition that can no longer
  // occur, which is this loop's stale comment in another costume.
  //
  // **IT NAMES WHAT WAS OBSERVED, NEVER THE CAUSE**, on the same discipline `EmploymentTypeAssumptionTests`
  // states for its own messages. Zero working days has at least three causes — the company has no working
  // calendar, the summary was unavailable, or the period genuinely contains no working day — and an error
  // asserting which one would be an instrument reporting on something it did not check.
  //
  // It fails the whole run rather than the employee, deliberately. A run that silently omitted one person's
  // pay would be discovered on payday.
  public static readonly Error DailySalaryHasNoWorkingDays = new(
    "Payroll.DailySalaryHasNoWorkingDays",
    "An employee is on a daily salary and this period reports no standard working days for the company, "
    + "so there is nothing to price a daily rate against.");

  // ---- A ONE-OFF NAMING AN ELEMENT THE RUN IS NOT PRICING (T-110).
  //
  // The run prices ACTIVE elements and never `NetPayPayable`. A one-off naming anything else would produce
  // no line — **an instruction somebody wrote, a person expecting money, and nothing anywhere.** That is the
  // exact defect T-110 exists to close, so the fix refuses rather than reproducing it one level in.
  //
  // It fails the RUN rather than the employee, like the daily refusal: a run that silently omitted one
  // person's pay would be discovered on payday.
  public static readonly Error OneOffPaymentElementNotPayable = new(
    "Payroll.OneOffPaymentElementNotPayable",
    "A one-off payment names a pay element this run is not pricing — it is inactive, or it is the net-pay "
    + "element, which is derived rather than configured.");

  // ---- A SECOND REVERSAL OF ONE POSTING (T-112).
  //
  // Two reversing entries for one posting, and the second timestamp would overwrite the record of when the
  // first happened. Refused rather than restamped.
  public static readonly Error RunAlreadyReversed = new(
    "Payroll.RunAlreadyReversed",
    "This payroll run has already been reversed.");

  public static readonly Error UnbalancedPosting = new(
    "Payroll.UnbalancedPosting",
    "The calculated run does not produce a balanced journal, which is a calculation defect rather than a user error.");
}
