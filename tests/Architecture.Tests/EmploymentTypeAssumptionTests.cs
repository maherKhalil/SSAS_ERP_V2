using System.Reflection;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THREE COMPUTATIONS ASSUME EVERY EMPLOYEE IS FULL-TIME. THESE FAIL THE DAY THAT STOPS BEING TRUE.
// ==================================================================================================
//
// Employment type does not exist in this product (T-068, verified 2026-08-27: zero occurrences of
// `EmploymentType`, `PartTime`, `FullTime`, `Contractor`, `Freelance`, `Fte` or `WorkingRatio` in
// `src/` or `tests/`). Three computations nonetheless depend on its absence, and **two of them are
// already wrong for a part-time employee in a way that announces nothing.**
//
// ---- THESE DO NOT DETECT A PART-TIME EMPLOYEE. NOTHING IN THE MODEL CAN.
//
// They assert the ASSUMPTION instead: that each computation has exactly the inputs it has, and that
// those inputs cannot express **a ratio, a schedule, or a policy** — the three shapes T-068 found
// wearing one name, which are not derivable from one another. `0.6` FTE does not tell you which days;
// which days does not tell you the pay ratio, because a compressed four-day full-time week is 1.0 pay
// across four days; and neither tells you whether a contractor accrues leave, which is a policy.
//
// ---- WHY THEY ARE NOT UNIFORM, WHICH IS DELIBERATE AND WILL LOOK LIKE AN OVERSIGHT.
//
// Guard 1 is broad, guard 3 is broad for a different reason, and guard 2 is deliberately narrow.
// **Making them uniform would make two of them wrong.** Each explains its own breadth below, because
// the obvious tidying — tighten 1, or widen 2 for symmetry — is the change to resist.
//
// ---- WHAT THEIR MESSAGES MAY AND MAY NOT SAY.
//
// They fail on ANY change to the shapes they assert, not only on employment type. So a message must
// state **the question, never the cause**: *this computation assumes full-time, the model may now
// express otherwise, here is what to check.* A guard asserting a cause it cannot know is an
// instrument reporting on a domain nobody established — the defect this repository spent a week
// removing — committed in prose instead of in code.
public sealed class EmploymentTypeAssumptionTests
{
  private static string[] ComponentsOf(Type recordType) =>
    [.. recordType.GetConstructors()
        .OrderByDescending(c => c.GetParameters().Length)
        .First()
        .GetParameters()
        .Select(p => p.Name!)];

  // ================================================================================================
  // GUARD 1 — PAYROLL. BROAD, AND ITS BREADTH IS THE PROPERTY RATHER THAN OVER-REACH.
  //
  // A ratio would arrive in `PayrollEmployeeInput` as a new component. **There is no narrower door**:
  // the component set IS the question, so asserting all of it is exactly right.
  //
  // WHAT IS AT STAKE. `PayrollCalculator.ProrationFactor` is `employedDays / periodDays` and there is
  // no other factor, so a part-timer's pay is correct **only if a human encoded the ratio into
  // `BaseAmount`** — and nothing records that they did. A 0.6 FTE employee on a full-time figure and
  // a full-time employee are identical to every query in the system.
  //
  // ---- THIS GUARD FIRED IN T-107, AND HERE IS THE ANSWER IT ASKED FOR.
  //
  // `WorkedQuantity` was added as a component. **It is not a ratio, a schedule or a policy: it is
  // HOURS ACTUALLY ATTENDED, an observed quantity rather than a contractual dimension.** It says what
  // someone did, never what they were engaged to do — 0.6 FTE and a full-timer who took two weeks off
  // produce the same number, which is precisely why it cannot express the thing this guard watches for.
  //
  // **And it narrows what is at stake above, for one salary type only.** An `Hourly` employee's pay is
  // `rate x hours attended`, so a part-timer is paid correctly BY CONSTRUCTION and no human has to
  // encode a ratio anywhere. `Monthly` is unchanged and the paragraph above still describes it exactly.
  //
  // ---- AND AGAIN IN T-108, WITH `StandardWorkingDays`.
  //
  // **Also not a ratio, a schedule or a policy: it is the COMPANY's working-day count for the period**, the
  // same number for every employee on that calendar. See guard 3, which asked the same question of the
  // contract this arrives on.
  //
  // **It narrows what is at stake above for `Daily` as well.** A daily employee's base is `working days -
  // unpaid days` at their own rate, so no human encodes a ratio for them either. **`Monthly` is the only
  // type the paragraph above still describes, and it describes it exactly.**
  //
  // ---- AND AGAIN IN T-110, WITH `OneOffPayments` — AND `Compensation` BECOMING NULLABLE.
  //
  // **A one-off pay instruction is an amount, an element and an identifier.** It carries no rate, no
  // schedule and no policy: two employees with identical instructions are paid identically. **Nothing about
  // it can encode how much of a full week somebody works.**
  //
  // **The nullable `Compensation` is the more interesting half, and it does NOT widen this guard's risk.**
  // A null means the employee is on no rate at all — no base, no proration, no absence deduction — and
  // `ProrationFactor` never touches their pay. **The paragraph above is about an employee whose ratio was
  // hand-encoded into `BaseAmount`; an employee with no `BaseAmount` has nowhere to hide one.**
  //
  // The assertion stays EXACT-EQUALITY. Relaxing it to "contains" would answer this question once and
  // never ask it again, which is the whole value of a tripwire that fires on any shape change.
  // ================================================================================================
  [Fact]
  public void Payroll_input_cannot_express_a_working_ratio_or_a_schedule()
  {
    string[] expected =
    [
      "EmployeeId", "HiredUtc", "TerminatedUtc",
      "Compensation", "OvertimeQuantityByTier", "UnpaidAbsenceQuantity", "WorkedQuantity",
      "StandardWorkingDays", "OneOffPayments"
    ];

    Assert.Equal(expected, ComponentsOf(typeof(PayrollEmployeeInput)));
  }

  // ================================================================================================
  // GUARD 2 — LEAVE. NARROW, AND NARROW BECAUSE IT IS EXACTLY RIGHT, NOT BECAUSE IT IS CHEAPER.
  //
  // The property is that **an employee dimension cannot enter the day count**. It has exactly two
  // doors: the constructor, if the calendar stops being company-scoped, and the method signature, if
  // the count starts taking an employee. Asserting those two is COMPLETE on the question.
  //
  // Asserting the whole public surface would cover more than the property, and every extra member it
  // covered would be a false alarm about something that was never employment type — coverage of a
  // domain the guard was not written to police, which is the shape this file exists to catch.
  //
  // WHAT IS AT STAKE, AND IT IS THE ONE A READER WILL NOT INFER. Leave consumption is counted in
  // COMPANY working days and **stored on the request, never recomputed** — correctly, so a holiday
  // added later cannot alter a past decision (`AC-ATT-0019`). A part-timer taking a calendar week is
  // charged five days when they work three, and because the figure is immutable, **every leave record
  // already written keeps its wrong count forever. A fix does not reach backwards.**
  // ================================================================================================
  [Fact]
  public void The_working_calendar_is_company_scoped_and_the_day_count_takes_no_employee()
  {
    var scoping = typeof(WorkingCalendar)
      .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
      .OrderByDescending(c => c.GetParameters().Length)
      .First();

    Assert.Equal(
      new[] { typeof(Guid), typeof(Guid), typeof(WorkingCalendarName), typeof(WeekendPattern), typeof(bool) },
      scoping.GetParameters().Select(p => p.ParameterType));

    var counts = typeof(WorkingCalendar)
      .GetMethods(BindingFlags.Instance | BindingFlags.Public)
      .Where(m => m.Name == nameof(WorkingCalendar.WorkingDaysBetween))
      .ToList();

    // One overload, so a second cannot be added alongside it and quietly become the one leave calls.
    Assert.Single(counts);
    Assert.Equal(
      new[] { typeof(DateOnly), typeof(DateOnly) },
      counts[0].GetParameters().Select(p => p.ParameterType));
  }

  // ================================================================================================
  // GUARD 3 — THE CROSS-MODULE CONTRACT. BROAD, AND JUSTIFIED BY A RULING THAT ALREADY EXISTS.
  //
  // `AttendanceSummaryResult`'s component set IS `DEC-ATT-0004`: *"No money, no rate, no multiplier,
  // no currency — Attendance records how much, Payroll decides what it is worth."* **Anything
  // arriving here deserves scrutiny whether or not it is employment type**, so a red on an unrelated
  // addition is this guard working rather than misfiring — which is the opposite of guard 2's case.
  //
  // WHAT IS AT STAKE. `UnpaidAbsenceQuantity` is multiplied by a daily rate derived from a CALENDAR-DAY
  // divisor (`OD-ATT-0015`, owner-ruled, and internally consistent for a full-timer). Someone working
  // three days a week loses 1/30 of a month for missing a day that is 1/13 of their working month:
  // **under-deducted by exactly their unknown ratio, while the payslip still adds up and every line
  // remains defensible.**
  //
  // ---- THIS GUARD FIRED TWICE, AND THE SECOND TIME IT WAS A REMOVAL (T-108, THEN T-115).
  //
  // T-108 added `StandardWorkingDays` — a COMPANY-AND-PERIOD fact, the working days the calendar says the
  // period contains, before anything about the employee — so that a daily salary could be priced. **T-115
  // removed it, and the removal is the interesting half.**
  //
  // **It was the wrong quantity for the job.** A daily employee is paid for working days they were
  // EMPLOYED for, and a period total cannot express a window inside itself — so a joiner hired mid-period
  // was paid the whole period's working days, and a leaver was paid past their termination.
  //
  // **Payroll now asks `IAttendanceSummary.GetWorkingDaysAsync` for the clamped window instead**, supplying
  // employment dates it already holds. **The employee dimension never enters this contract** — which is the
  // thing this guard exists to prevent, and the fix is why the field LEFT rather than a reason to make it
  // employee-aware. **The alternative — clamping inside `GetForPeriodAsync` — was refused for exactly that
  // reason.**
  //
  // ---- WHICH SALARY TYPES THE PARAGRAPH ABOVE STILL DESCRIBES (T-109).
  //
  //   MONTHLY  IT STANDS, ENTIRELY AND UNCHANGED. A calendar-day divisor applied to a monthly amount, and
  //            someone working three days a week is under-deducted by exactly their unknown ratio.
  //   DAILY    CLOSED. The base is `working days - unpaid days` at the employee's own rate, and T-109
  //            excluded the element, so a missed day costs one day and nothing else touches it.
  //   HOURLY   NEVER APPLIED. Pay is attendance; there was never a deduction to be imprecise.
  //
  // ---- AND THE T-108 VERSION OF THIS PARAGRAPH WAS WRONG, WHICH IS WHY THE SPLIT IS SPELLED OUT.
  //
  // It divided the risk into BASE (fixed) and DEDUCTION (still wrong), and put daily's base on the fixed
  // side — **while the element was still firing on top of it.** Every clause was true about the base and
  // the conclusion a reader would draw was false: daily's NET over-deducted by the element's whole amount.
  //
  // **The axis is the salary type, not the base-versus-element.** A guard that under-states its own risk is
  // more dangerous than a stale comment, because it is read precisely when someone is deciding whether a
  // thing is safe.
  // ================================================================================================
  [Fact]
  public void The_attendance_summary_carries_quantities_and_cannot_carry_a_ratio()
  {
    string[] expected =
    [
      "Status", "EmployeeId", "CompanyId", "AttendancePeriodId",
      "PeriodStartUtc", "PeriodEndUtc", "WorkedQuantity",
      "OvertimeQuantityByTier", "PaidAbsenceQuantity", "UnpaidAbsenceQuantity"
    ];

    Assert.Equal(expected, ComponentsOf(typeof(AttendanceSummaryResult)));
  }
}
