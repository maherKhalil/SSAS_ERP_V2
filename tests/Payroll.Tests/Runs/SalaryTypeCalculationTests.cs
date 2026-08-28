using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests.Runs;

// ================================================================================================
// WHAT A BASE AMOUNT MEANS, AND THE UNITS IT IS MULTIPLIED BY (T-107).
// ================================================================================================
//
// ---- WHY THIS FILE EXISTS RATHER THAN MORE CASES IN THE TWO NEIGHBOURS.
//
// `PayrollCalculatorTests` carries the monthly model and had to pass **with no edit at all** for the salary
// type to be a safe addition — a default that required touching an existing expectation would not have been
// a default. Its fourteen scenarios and `AttendanceDrivenCalculationTests`' nine are untouched.
//
// ---- THE UNITS, AND WHY ASSERTING THEM IS THE POINT OF THIS FILE.
//
// Three quantities reach the calculator from the SAME attendance record, in TWO DIFFERENT UNITS:
//
//   OvertimeQuantityByTier   HOURS
//   WorkedQuantity           HOURS
//   UnpaidAbsenceQuantity    DAYS
//
// **Before T-107 every one of those units lived in a prose comment and was asserted nowhere** — no
// validation, no test, no schema constraint, no criterion. An hourly rate multiplied by a day count is a
// payslip that balances, posts, and is wrong by roughly a factor of eight; nothing in the product would have
// noticed.
//
// Each case below is arithmetic chosen so that **assuming the other unit gives a different number**. That is
// what makes them assertions of a unit rather than of a total.
public sealed class SalaryTypeCalculationTests
{
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid SalaryAccount = Guid.NewGuid();
  private static readonly Guid AllowanceAccount = Guid.NewGuid();
  private static readonly Guid AbsenceAccount = Guid.NewGuid();
  private static readonly Guid OvertimeAccount = Guid.NewGuid();

  private static readonly DateTimeOffset LongEmployed = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

  // ---- THE DEFAULT IS MONTHLY, AND IT IS WHAT MAKES EVERY EXISTING ROW SAFE.
  //
  // `SalaryType.Monthly` is the enum's ZERO. A column added to a table full of rows reads back as 0 for all
  // of them, and the migration writes `defaultValue: 0` — so every compensation record written before this
  // feature existed means exactly what it always meant, at the database as well as in code.
  [Fact]
  public void A_compensation_record_that_names_no_salary_type_is_monthly()
  {
    var record = EmployeeCompensation.Create(
      Guid.NewGuid(), Employee, LongEmployed, 5000m);

    Assert.True(record.IsSuccess);
    Assert.Equal(SalaryType.Monthly, record.Value.SalaryType);
  }

  // ---- HOURLY: THE BASE IS THE RATE TIMES HOURS ATTENDED.
  //
  // 20/hour x 160 hours = 3200. **If `WorkedQuantity` were read as DAYS the answer would be 20 x 20 = 400**,
  // so this case fails loudly under the wrong unit rather than merely being unverified.
  [Fact]
  public void An_hourly_salary_is_the_rate_times_the_hours_attended()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var lines = Calculate(Hourly(20m), [basic], workedQuantity: 160m);

    Assert.Equal(3200m, Assert.Single(lines).Amount);
  }

  // ---- HOURLY IS NOT PRORATED, AND A MID-PERIOD JOINER IS THE PROOF.
  //
  // `OD-PAY-0015` ruled calendar-day proration for the MONTHLY model. A rate times hours attended has
  // already answered "how much of the period was this employee here for" — multiplying by the fraction
  // again would pay someone who joined on the 24th for a quarter of the hours they actually worked.
  //
  // Hired on the 24th of a 31-day period: the monthly factor would be 8/31. The answer must be unaffected.
  [Fact]
  public void An_hourly_salary_is_not_prorated_for_a_mid_period_joiner()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var period = PayrollTestData.Period();

    var full = Calculate(Hourly(20m), [basic], workedQuantity: 40m, hired: LongEmployed);
    var joiner = Calculate(Hourly(20m), [basic], workedQuantity: 40m, hired: period.StartUtc.AddDays(23));

    Assert.Equal(800m, Assert.Single(full).Amount);
    Assert.Equal(800m, Assert.Single(joiner).Amount);
  }

  // ---- AN HOURLY SALARY TAKES NO UNPAID-ABSENCE DEDUCTION (owner-ruled, T-107).
  //
  // **An hourly employee is paid only for the time they attend**, so the absence has already been priced by
  // not appearing in `WorkedQuantity`. Deducting again charges them twice for one absence — and the
  // deduction's divisor is the period's CALENDAR days, which against an hourly rate means nothing at all.
  //
  // The employee below worked 100 hours and has 5 unpaid DAYS recorded. The deduction is not merely small;
  // it is absent, and the zero-line rule then suppresses it.
  [Fact]
  public void An_hourly_salary_takes_no_unpaid_absence_deduction()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    var lines = Calculate(
      Hourly(20m), [basic, absence], workedQuantity: 100m, unpaidAbsenceQuantity: 5m);

    Assert.Equal(2000m, lines.Single(line => line.PayElementId == basic.Id).Amount);
    Assert.DoesNotContain(lines, line => line.PayElementId == absence.Id);
  }

  // ---- THE SAME QUANTITY, ON A MONTHLY SALARY, IS DAYS AND DOES DEDUCT.
  //
  // This is the other half of the unit assertion and it is the reason the pair is worth having: the SAME
  // field, 5, means five DAYS here and is priced by the calendar-day rate.
  //
  // 3100 over a 31-day period is 100 a day; five unpaid days is 500. **Were the quantity read as hours the
  // deduction would still be 500 and the test would pass** — which is exactly why the hourly case above
  // carries the unit and this one carries the arithmetic. Neither proves the unit alone.
  [Fact]
  public void The_unpaid_absence_quantity_is_priced_as_days_on_a_monthly_salary()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    var lines = Calculate(Monthly(3100m), [basic, absence], unpaidAbsenceQuantity: 5m);

    Assert.Equal(3100m, lines.Single(line => line.PayElementId == basic.Id).Amount);
    Assert.Equal(500m, lines.Single(line => line.PayElementId == absence.Id).Amount);
  }

  // ---- OVERTIME IS HOURS, AND IT IS HOURS ON AN HOURLY SALARY TOO.
  //
  // `OvertimeHourly` was already quantity x rate before T-107 — the product's first hourly rate, and the one
  // that showed a salary type needed no new BEHAVIOUR, only a new base. Six hours at 30 is 180 whatever the
  // employee's salary type is, because overtime prices what was worked rather than what was contracted.
  [Fact]
  public void Overtime_is_priced_in_hours_regardless_of_salary_type()
  {
    var overtime = PayrollTestData.Element(
      "OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 30m, 10, OvertimeAccount);
    var withTier = overtime.SetOvertimeTier("NIGHT");
    Assert.True(withTier.IsSuccess, withTier.IsFailure ? withTier.Error.Message : string.Empty);

    var tiers = new Dictionary<string, decimal> { ["NIGHT"] = 6m };

    var hourly = Calculate(
      Hourly(20m, (overtime.Id, null)), [overtime], workedQuantity: 10m, overtimeByTier: tiers);
    var monthly = Calculate(
      Monthly(3100m, (overtime.Id, null)), [overtime], overtimeByTier: tiers);

    Assert.Equal(180m, hourly.Single(line => line.PayElementId == overtime.Id).Amount);
    Assert.Equal(180m, monthly.Single(line => line.PayElementId == overtime.Id).Amount);
  }

  // ---- A PERCENTAGE ELEMENT ON AN HOURLY SALARY FOLLOWS THE HOURS (owner-ruled trap 5, T-107).
  //
  // `PercentageOfBaseSalary` computes off `baseAmount`, which is already the prorated figure in the monthly
  // model — so a percentage allowance has ALWAYS scaled with how much base was actually earned. Under an
  // hourly salary that means it scales with hours worked.
  //
  // **That is the same rule, not a new one — but it was invisible until someone asked**, so it is asserted
  // here rather than left to be discovered on a payslip. Double the hours, double the allowance.
  [Fact]
  public void A_percentage_element_on_an_hourly_salary_follows_the_hours()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var allowance = PayrollTestData.Element(
      "HOUSING", PayElementKind.Earning, PayElementBehaviour.PercentageOfBaseSalary, 10m, 20, AllowanceAccount);

    var short_ = Calculate(
      Hourly(20m, (allowance.Id, null)), [basic, allowance], workedQuantity: 80m);
    var long_ = Calculate(
      Hourly(20m, (allowance.Id, null)), [basic, allowance], workedQuantity: 160m);

    Assert.Equal(160m, short_.Single(line => line.PayElementId == allowance.Id).Amount);
    Assert.Equal(320m, long_.Single(line => line.PayElementId == allowance.Id).Amount);
  }

  // ---- DAILY IS THE PERIOD'S WORKING DAYS, NOT THE DAYS ACTUALLY WORKED (T-108, owner-ruled).
  //
  // **The rejected model is the one the phrase "daily rate" invites**: `rate x days actually worked`. Under
  // it, absence is already excluded and the `UnpaidAbsenceDeduction` element would take it a second time.
  //
  // The owner excluded HOURLY from that deduction because an hourly employee is paid only for the time they
  // attend, and INCLUDED daily. **That contrast is only meaningful if a daily employee is not paid solely
  // for time attended** — so the base is the period's standard working days and the deduction takes the
  // unpaid ones back.
  //
  // 22 working days at 200 = 4400.
  [Fact]
  public void A_daily_salary_is_the_rate_times_the_periods_standard_working_days()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var lines = Calculate(Daily(200m), [basic], standardWorkingDays: 22);

    Assert.Equal(4400m, Assert.Single(lines).Amount);
  }

  // ---- THE UNPAID DAYS COME OFF THE BASE, AND THE ELEMENT DOES NOT FIRE (T-109).
  //
  // **This is the regression test for T-108's defect, and it must assert the ABSENCE of the deduction
  // rather than only the base.** T-108 shipped this same base — 3800 — and let the element fire on top of
  // it; a test that checked only the base would have passed against the defect and would pass against it
  // again.
  //
  // 22 working days less 3 unpaid is 19 at 200 = 3800, which is exactly what nineteen days cost. Under
  // T-108 the element then took 3800/31 x 3 = 367.74, leaving **3432.26 for nineteen days that cost 3800.**
  //
  // The company here HAS configured an unpaid-absence element, which is the only configuration in which
  // the defect was visible — the element takes no per-employee assignment, so defining it once affected
  // every daily employee, and a company that never defined one was always correct.
  [Fact]
  public void A_daily_salary_prices_unpaid_days_in_the_base_and_takes_no_deduction()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    var lines = Calculate(
      Daily(200m), [basic, absence], standardWorkingDays: 22, unpaidAbsenceQuantity: 3m);

    Assert.Equal(3800m, lines.Single(line => line.PayElementId == basic.Id).Amount);
    Assert.DoesNotContain(lines, line => line.PayElementId == absence.Id);
  }

  // ---- AND MONTHLY KEEPS THE ELEMENT, WITH THE SAME ELEMENT AND THE SAME QUANTITY.
  //
  // **The pair is the point.** T-109 changed which salary types the deduction applies to, and a test that
  // only proved daily's exemption would not show that monthly still takes it — leaving "removed the
  // deduction" and "removed it for daily" indistinguishable.
  //
  // 3100 over 31 calendar days is 100 a day; three unpaid days is 300.
  [Fact]
  public void A_monthly_salary_still_takes_the_deduction_the_daily_one_no_longer_does()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    var lines = Calculate(Monthly(3100m), [basic, absence], unpaidAbsenceQuantity: 3m);

    Assert.Equal(3100m, lines.Single(line => line.PayElementId == basic.Id).Amount);
    Assert.Equal(300m, lines.Single(line => line.PayElementId == absence.Id).Amount);
  }

  // ---- A DAILY SALARY IS NOT PRORATED EITHER.
  //
  // The working-day count is the period's, so a mid-period joiner is paid the same as anyone else on the
  // same calendar. **That is a consequence of the owner's model worth asserting rather than discovering**:
  // under `rate x days ACTUALLY worked` a joiner would be paid less, and under this model they are not.
  [Fact]
  public void A_daily_salary_is_not_prorated_for_a_mid_period_joiner()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var period = PayrollTestData.Period();
    var joiner = Calculate(
      Daily(200m), [basic], standardWorkingDays: 22, hired: period.StartUtc.AddDays(23));

    Assert.Equal(4400m, Assert.Single(joiner).Amount);
  }

  // ---- WITH NO WORKING DAYS THE RUN REFUSES RATHER THAN PAYING ZERO.
  //
  // Zero working days has at least three causes — no working calendar for the company, an unavailable
  // summary, or a period genuinely containing none — and `PayrollErrors.DailySalaryHasNoWorkingDays` names
  // what was OBSERVED rather than which occurred.
  //
  // It fails the RUN rather than the employee: a run that silently paid one person zero would be discovered
  // on payday.
  [Fact]
  public void A_daily_salary_with_no_working_days_refuses_the_run()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var result = PayrollCalculator.Calculate(
      Guid.NewGuid(), PayrollTestData.Period(),
      [new PayrollEmployeeInput(Employee, LongEmployed, null, Daily(200m), null, 0m, 160m, 0)],
      [basic]);

    Assert.True(result.IsFailure);
    Assert.Equal(PayrollErrors.DailySalaryHasNoWorkingDays.Code, result.Error.Code);
  }

  private static EmployeeCompensation Monthly(
    decimal baseAmount, params (Guid Element, decimal? Rate)[] assignments) =>
    Record(baseAmount, SalaryType.Monthly, assignments);

  private static EmployeeCompensation Hourly(
    decimal baseAmount, params (Guid Element, decimal? Rate)[] assignments) =>
    Record(baseAmount, SalaryType.Hourly, assignments);

  private static EmployeeCompensation Daily(
    decimal baseAmount, params (Guid Element, decimal? Rate)[] assignments) =>
    Record(baseAmount, SalaryType.Daily, assignments);

  private static EmployeeCompensation Record(
    decimal baseAmount, SalaryType salaryType, (Guid Element, decimal? Rate)[] assignments)
  {
    var record = EmployeeCompensation.Create(
      Guid.NewGuid(), Employee, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), baseAmount,
      assignments.Select(assignment => (assignment.Element, assignment.Rate)).ToArray(),
      salaryType);

    Assert.True(record.IsSuccess, record.IsFailure ? record.Error.Message : string.Empty);
    return record.Value;
  }

  private static IReadOnlyList<PayrollRunDraftLine> Calculate(
    EmployeeCompensation compensation,
    PayElement[] elements,
    decimal workedQuantity = 0m,
    decimal unpaidAbsenceQuantity = 0m,
    IReadOnlyDictionary<string, decimal>? overtimeByTier = null,
    DateTimeOffset? hired = null,
    int standardWorkingDays = 0)
  {
    var input = new PayrollEmployeeInput(
      Employee, hired ?? LongEmployed, null, compensation,
      overtimeByTier, unpaidAbsenceQuantity, workedQuantity, standardWorkingDays);

    var result = PayrollCalculator.Calculate(
      Guid.NewGuid(), PayrollTestData.Period(), [input], elements);

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : string.Empty);
    return result.Value;
  }
}
