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
    "A payroll period must belong to a company.",
    Field: "companyId");

  public static readonly Error PeriodFiscalPeriodRequired = new(
    "Payroll.PeriodFiscalPeriodRequired",
    "A payroll period must be aligned to a fiscal period.");

  public static readonly Error PeriodNameInvalid = new(
    "Payroll.PeriodNameInvalid",
    "A payroll period name is required and must be at most 128 characters.",
    Field: "name");

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
    "A payroll run must belong to a company.",
    Field: "companyId");

  public static readonly Error RunPeriodRequired = new(
    "Payroll.RunPeriodRequired",
    "A payroll run must name a payroll period.",
    Field: "payrollPeriodId");

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

  // ---- 249. THE LEDGER IS BUSY, NOT REFUSING. THE RUN SHOULD BE REPEATED.
  //
  // ⚠ THE MESSAGE DOES THE WORK, NOT THE NAME. A retryable refusal whose text reads like a failure has
  // kept none of its value: an operator told "the ledger refused this posting" stops and investigates,
  // which is the wrong action for a condition that clears on its own.
  public static readonly Error LedgerPostingRetryable = new(
    "Payroll.LedgerPostingRetryable",
    "The fiscal period's state is being changed right now, so this run was not posted. Try posting it again.");

  // ---- 254. NO FISCAL PERIOD COVERS THE DATE, WHICH IS NOT THE SAME AS A REFUSAL.
  //
  // `JournalPostingStatus.PeriodNotFound`'s own comment is the argument for this member: *distinct from
  // closed, because the operator's remedy is different: define the calendar rather than reopen a period.*
  // Collapsed into the generic refusal, that remedy is unreachable — "the ledger refused this posting"
  // tells an operator to investigate a posting that was never attempted against any period.
  //
  // ⚠ IT IS NOT `FiscalPeriodNotFound`, WHICH ALREADY EXISTS AND IS DELIBERATELY NOT REUSED. That one is
  // the APPROVAL-time calendar check, it says "cannot be approved", and it maps to a GENERIC 404 that
  // `PayrollEndpointTests` already pins as a poor answer. Reusing it would carry both defects into a new
  // site and put an approval message on a posting failure.
  public static readonly Error LedgerHasNoFiscalPeriod = new(
    "Payroll.LedgerHasNoFiscalPeriod",
    "No fiscal period covers this date, so no ledger entry was made. Define the fiscal year that covers " +
    "it, then try again.");

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

  // ---- AN EMPLOYEE'S ATTENDANCE DISAGREES WITH THEIR EMPLOYMENT DATES (T-121).
  //
  // Work recorded on days they were not employed. **Either they worked and the termination date is wrong,
  // or they did not and the record is** — and neither module can tell which, so neither chooses.
  //
  // It fails the RUN rather than the employee, on the same reasoning as `DailySalaryHasNoWorkingDays`: a
  // run that silently omitted or inflated one person's pay would be discovered on payday.
  public static readonly Error AttendanceContradictsEmployment = new(
    "Payroll.AttendanceContradictsEmployment",
    "An employee has attendance recorded on days they were not employed. Correct the attendance records or "
    + "the employment dates before calculating this run.");

  // ---- OVERTIME WORKED AGAINST A TIER THE EMPLOYEE'S ELEMENTS DO NOT PRICE (T-149).
  //
  // Attendance recorded hours under a tier label; the employee IS paid overtime — they hold at least one
  // `OvertimeHourly` assignment — but none of their assigned elements names that tier. **The hours exist and
  // nothing prices them.**
  //
  // Refused rather than paid as zero, on the precedent this module has set twice:
  // `DailySalaryHasNoWorkingDays` refuses rather than paying nothing for a day, and
  // `AttendanceContradictsEmployment` refuses before a quantity is read. **A zero is indistinguishable from
  // an employee who worked no overtime**, and it reaches a payslip looking complete.
  //
  // ---- ⚠ IT DOES NOT FIRE FOR AN EMPLOYEE WITH NO OVERTIME ELEMENTS AT ALL, AND THAT IS DELIBERATE.
  //
  // The calculator's own rule is that an unassigned `OvertimeHourly` element means *"this employee is not
  // paid overtime"* — **a legitimate standing instruction, not a misconfiguration.** Refusing there would
  // break a supported setup. **This fires only when the employee IS paid overtime and a tier they worked is
  // not among the tiers their elements name.**
  public static readonly Error OvertimeTierHasNoPricedElement = new(
    "Payroll.OvertimeTierHasNoPricedElement",
    "An employee worked overtime under a tier none of their assigned overtime elements prices. Assign an "
    + "element for that tier, or correct the tier on the attendance records, before calculating this run.");

  // ---- A CONTRACT EMPLOYEE TAKES NO COMPENSATION RECORD AT ALL (T-153).
  //
  // `EmployeeCompensation` prices a RECURRING engagement: every `SalaryType` it can express — `Monthly`,
  // `Daily`, `Hourly` — is a rate the run multiplies out each period. **A contract engagement is paid
  // through `OneOffPayment`, which is a different mechanism with its own approval and its own errors.**
  //
  // So this is not "contract employees may only use salary type X". **There is no X.** Recording a
  // compensation record for a contract employee would make them recur in every run, which is precisely
  // the outcome the engagement type exists to prevent.
  //
  // ⚠ **The refusal is on the pairing, not on the employee.** A contract employee who converts to
  // full-time takes a compensation record the moment HR changes the type, and this stops firing with no
  // Payroll change at all.
  public static readonly Error CompensationNotAvailableForContract = new(
    "Payroll.CompensationNotAvailableForContract",
    "This employee is engaged on a contract, and a contract engagement is paid through one-off payments "
    + "rather than a recurring compensation record. Record a one-off payment, or change the employment "
    + "type in HR first.");

  // ⚠ AND THE EMPLOYEE MUST EXIST. A null employment type says HR HAS NO SUCH EMPLOYEE, which is a
  // different fact from any type — collapsing it into "not a contract" would turn a missing row into a
  // silent grant, and compensation would be recorded against an id that names nobody.
  public static readonly Error CompensationEmployeeNotInHr = new(
    "Payroll.CompensationEmployeeNotInHr",
    "No employee with that identifier exists in HR for the current tenant, so no compensation can be "
    + "recorded against it.");

  public static readonly Error UnbalancedPosting = new(
    "Payroll.UnbalancedPosting",
    "The calculated run does not produce a balanced journal, which is a calculation defect rather than a user error.");
}
