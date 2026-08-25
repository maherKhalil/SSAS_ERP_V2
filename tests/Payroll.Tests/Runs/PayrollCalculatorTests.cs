using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests.Runs;

// THE CALCULATION ENGINE (OD-PAY-0007, OD-PAY-0008).
//
// Every test here runs against the real calculator with no database and no clock, because the calculator
// takes neither. That is not a convenience — a calculation that varied with ambient state could not be
// reproduced, and `BR-PLT-0103` treats payroll processing as sensitive precisely because it must be
// answerable after the fact.
public sealed class PayrollCalculatorTests
{
  private static readonly Guid SalaryAccount = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private static readonly Guid AllowanceAccount = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
  private static readonly Guid DeductionAccount = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

  private static readonly Guid Employee = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

  [Fact]
  [Trait("Decision", "OD-PAY-0008")]
  public void The_payslip_adds_up_because_the_total_is_the_sum_of_rounded_lines()
  {
    // ---- THE INVARIANT THE ROUNDING RULING EXISTS TO PROTECT.
    //
    // 3333.335 and a 7.5% allowance both land on a half, so both round. Under `OD-PAY-0008` each LINE is
    // rounded and the total is their sum, so the lines a person can see add up to the total they are paid.
    // Under the rejected option — full precision, round only the net — they would not, and that reads as a
    // bug forever.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var allowance = PayrollTestData.Element(
      "HOUSING", PayElementKind.Earning, PayElementBehaviour.PercentageOfBaseSalary, 7.5m, 1, AllowanceAccount);

    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(
      Employee, period.StartUtc.AddYears(-1), 3333.335m, (allowance.Id, null));

    var lines = Calculate(period, compensation, basic, allowance);

    Assert.All(lines, line => Assert.Equal(line.Amount, Math.Round(line.Amount, 2)));

    var run = ApprovedRun(period, lines);
    Assert.Equal(run.Lines.Sum(line => line.Amount), run.TotalEarnings + run.TotalDeductions);
    Assert.Equal(run.TotalEarnings - run.TotalDeductions, run.NetPay);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0008")]
  public void Rounding_is_half_away_from_zero_not_to_even()
  {
    // Banker's rounding was explicitly rejected. 0.125 at 2dp is 0.13 away-from-zero and 0.12 to-even, so
    // this distinguishes the two rather than merely asserting "it rounds".
    Assert.Equal(0.13m, PayrollCalculator.RoundLine(0.125m));
    Assert.Equal(0.14m, PayrollCalculator.RoundLine(0.135m));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0007")]
  public void The_same_inputs_produce_identical_lines_every_time()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(Employee, period.StartUtc.AddYears(-1), 5000m);

    var first = Calculate(period, compensation, basic);
    var second = Calculate(period, compensation, basic);

    Assert.Equal(
      first.Select(line => (line.EmployeeId, line.PayElementId, line.Amount, line.Sequence)),
      second.Select(line => (line.EmployeeId, line.PayElementId, line.Amount, line.Sequence)));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0007")]
  public void Elements_evaluate_in_ascending_order_and_a_later_one_sees_an_earlier_result()
  {
    // `PercentageOfGrossToDate` is the behaviour that makes ordering load-bearing rather than decorative:
    // it reads the earnings accumulated so far, so moving it changes its result legitimately.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, order: 0, account: SalaryAccount);
    var bonus = PayrollTestData.Element(
      "BONUS", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 1000m, 1, AllowanceAccount);
    var levy = PayrollTestData.Element(
      "LEVY", PayElementKind.Deduction, PayElementBehaviour.PercentageOfGrossToDate, 10m, 2, DeductionAccount);

    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(
      Employee, period.StartUtc.AddYears(-1), 4000m, (bonus.Id, null), (levy.Id, null));

    var lines = Calculate(period, compensation, basic, bonus, levy);

    // The levy sees basic + bonus = 5000, so 10% is 500. If it had evaluated first it would have seen zero
    // and produced no line at all.
    var levyLine = Assert.Single(lines, line => line.PayElementId == levy.Id);
    Assert.Equal(500m, levyLine.Amount);
    Assert.True(levyLine.Sequence > lines.Single(line => line.PayElementId == bonus.Id).Sequence);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0007")]
  public void A_mid_period_joiner_is_prorated_by_calendar_days_at_both_boundaries()
  {
    // ---- BOTH BOUNDARIES, BECAUSE OFF-BY-ONE AT A BOUNDARY IS THE DEFECT THIS METHOD EXISTS TO GET RIGHT.
    //
    // A 31-day period. Hired on the LAST day is paid for one day; hired on the FIRST day is paid in full.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(Employee, period.StartUtc.AddYears(-1), 3100m);

    var lastDay = Calculate(period, compensation, basic, hired: period.EndUtc);
    Assert.Equal(100m, Assert.Single(lastDay).Amount);

    var firstDay = Calculate(period, compensation, basic, hired: period.StartUtc);
    Assert.Equal(3100m, Assert.Single(firstDay).Amount);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0010")]
  public void An_employee_terminated_within_the_period_is_paid_for_the_days_worked()
  {
    // `BR-HR-0004` bars NEW obligations, not the settlement of obligations already incurred. Final pay is a
    // settlement — the literal reading would mean people do not receive it.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(Employee, period.StartUtc.AddYears(-1), 3100m);

    var lines = Calculate(
      period, compensation, basic,
      hired: period.StartUtc.AddYears(-1), terminated: period.StartUtc);

    Assert.Equal(100m, Assert.Single(lines).Amount);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0016")]
  public void No_statutory_deduction_is_produced_because_none_can_be()
  {
    // V1 IS JURISDICTION-NEUTRAL. The net figure is gross minus CONFIGURED deductions and nothing else — no
    // bracket, no table, no liability. This asserts the boundary rather than merely documenting it: a future
    // behaviour that computed one would have to delete this test.
    Assert.DoesNotContain(
      Enum.GetNames<PayElementBehaviour>(),
      name => name.Contains("Tax", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Statutory", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Bracket", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0002")]
  public void No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt()
  {
    Assert.DoesNotContain(
      Enum.GetNames<PayElementBehaviour>(),
      name => name.Contains("Hour", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Overtime", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Absen", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void The_net_pay_payable_element_produces_no_line_because_net_pay_is_derived()
  {
    // The one place an element is not a line. It carries the mapping for the balancing credit; a line would
    // double-count, because net pay is computed FROM the other lines.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var payable = PayrollTestData.Element(
      "NETPAY", PayElementKind.Deduction, PayElementBehaviour.NetPayPayable, 0m, 99, DeductionAccount);

    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(Employee, period.StartUtc.AddYears(-1), 5000m);

    var lines = Calculate(period, compensation, basic, payable);

    Assert.DoesNotContain(lines, line => line.PayElementId == payable.Id);
  }

  [Fact]
  public void An_element_the_employee_is_not_assigned_produces_no_line_at_all()
  {
    // Absent and zero are different facts. A zero line would clutter every payslip with things that do not
    // apply to the person reading it.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var unassigned = PayrollTestData.Element(
      "CAR", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 500m, 1, AllowanceAccount);

    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(Employee, period.StartUtc.AddYears(-1), 5000m);

    var lines = Calculate(period, compensation, basic, unassigned);

    Assert.DoesNotContain(lines, line => line.PayElementId == unassigned.Id);
  }

  [Fact]
  public void An_inactive_element_is_excluded_rather_than_refusing_the_run()
  {
    // Deactivating an element is how a company stops using it. A run that refused to calculate because a
    // no-longer-used element exists would be unusable.
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var retired = PayrollTestData.Element(
      "OLD", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 500m, 1, AllowanceAccount);
    retired.Deactivate();

    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(
      Employee, period.StartUtc.AddYears(-1), 5000m, (retired.Id, null));

    var lines = Calculate(period, compensation, basic, retired);

    Assert.DoesNotContain(lines, line => line.PayElementId == retired.Id);
    Assert.Single(lines);
  }

  [Fact]
  public void A_period_with_nobody_employed_refuses_rather_than_producing_an_empty_run()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var period = PayrollTestData.Period();
    var compensation = PayrollTestData.Compensation(Employee, period.StartUtc.AddYears(-1), 5000m);

    // Hired after the period ended.
    var result = PayrollCalculator.Calculate(
      Guid.NewGuid(), period,
      [PayrollTestData.Employee(Employee, period.EndUtc.AddDays(1), null, compensation)],
      [basic]);

    Assert.True(result.IsFailure);
    Assert.Equal(PayrollErrors.NoIncludedEmployees.Code, result.Error.Code);
  }

  private static IReadOnlyList<PayrollRunDraftLine> Calculate(
    PayrollPeriod period,
    Domain.Compensation.EmployeeCompensation compensation,
    params PayElement[] elements) =>
    Calculate(period, compensation, elements, period.StartUtc.AddYears(-1), null);

  private static IReadOnlyList<PayrollRunDraftLine> Calculate(
    PayrollPeriod period,
    Domain.Compensation.EmployeeCompensation compensation,
    PayElement element,
    DateTimeOffset hired,
    DateTimeOffset? terminated = null) =>
    Calculate(period, compensation, [element], hired, terminated);

  private static IReadOnlyList<PayrollRunDraftLine> Calculate(
    PayrollPeriod period,
    Domain.Compensation.EmployeeCompensation compensation,
    PayElement[] elements,
    DateTimeOffset hired,
    DateTimeOffset? terminated)
  {
    var result = PayrollCalculator.Calculate(
      Guid.NewGuid(), period,
      [PayrollTestData.Employee(Employee, hired, terminated, compensation)],
      elements);

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : string.Empty);
    return result.Value;
  }

  private static PayrollRun ApprovedRun(PayrollPeriod period, IReadOnlyList<PayrollRunDraftLine> lines)
  {
    var run = PayrollTestData.Run(period.Id);
    Assert.True(run.SetCalculation(lines, "tester").IsSuccess);
    Assert.True(run.Approve("approver").IsSuccess);
    return run;
  }
}
