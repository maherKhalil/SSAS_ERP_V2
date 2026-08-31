using SSAS.Attendance.Contracts.Summaries;
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
  // ⚠ CITED BY B18 pass 15, body-confirmed: ⚠ PARTLY PINNED, and the clause names matter here.
  //
  // `AC-PAY-0026` is *"a payslip returns the stored lines FOR ONE EMPLOYEE IN ONE RUN, and the lines SUM
  // EXACTLY to the stated total"*. **This asserts the SUM clause only** -- the lines add to
  // `TotalEarnings + TotalDeductions` and the net is their difference.
  //
  // ⚠ The RETRIEVAL clause belongs to `PayrollReadService.GetPayslipAsync(scope, runId, employeeId)`, and
  // **nothing constructs that class in any suite** -- so the filter that makes a payslip one employee's
  // and one run's is pinned by nothing. Recorded rather than implied, and queued as its own item.
  [Trait("Criterion", "AC-PAY-0026")]
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
  // ⚠ CITED BY B18 pass 15, body-confirmed: BOTH CLAUSES of `AC-PAY-0009`.
  //
  // Clause 1 -- *elements are evaluated in ascending calculation order* -- is asserted where the order is
  // LOAD-BEARING rather than decorative: `PercentageOfGrossToDate` reads the earnings accumulated so far,
  // so the levy seeing basic + bonus = 5000 and producing 500 is only possible if it ran third.
  //
  // ⚠ Clause 2 -- *and a line records the order used* -- is the `Sequence` comparison on the last line.
  // It is a separate claim from clause 1 and would be missed by a reader who stopped at the amount.
  [Trait("Criterion", "AC-PAY-0009")]
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
  // ================================================================================================
  // REPLACED, NOT DELETED (FP-013, DEC-ATT-0012).
  // ================================================================================================
  //
  // This slot held `No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt`, which asserted
  // that no `PayElementBehaviour` name contained *Hour*, *Overtime* or *Absence*. It guarded `DEC-PAY-0002`:
  // overtime and absence deduction could not exist because the INPUT did not exist.
  //
  // **FP-013 built the input, so the guard went red — correctly.** The fact it protected has changed.
  //
  // It is REPLACED rather than deleted, on exactly the pattern FP-012 used when GL's vacuous
  // `There_is_no_gl_contracts_assembly` was superseded by two guards that load the assembly by reference.
  // The two successors below assert the NEW positive truth, so the principle `DEC-PAY-0002` expressed —
  // **no behaviour without an input** — is preserved rather than discarded along with its old wording.
  //
  // **A green suite obtained by deleting the test that went red is not a green suite.**

  [Fact]
  [Trait("Decision", "DEC-ATT-0012")]
  [Trait("Requirement", "REQ-ATT-0022")]
  public void The_attendance_driven_behaviours_exist_now_that_attendance_supplies_them()
  {
    // The positive half. `DEC-PAY-0002` is lifted for exactly two behaviours, and both have a declared
    // input on `AttendanceSummaryResult`.
    var names = Enum.GetNames<PayElementBehaviour>();

    Assert.Contains(nameof(PayElementBehaviour.OvertimeHourly), names);
    Assert.Contains(nameof(PayElementBehaviour.UnpaidAbsenceDeduction), names);

    // And the inputs they consume are actually on the contract — not merely believed to be. A behaviour
    // whose named input had been renamed away would still pass a name-only assertion.
    var summary = typeof(AttendanceSummaryResult).GetProperties().Select(property => property.Name).ToArray();
    Assert.Contains(nameof(AttendanceSummaryResult.OvertimeQuantityByTier), summary);
    Assert.Contains(nameof(AttendanceSummaryResult.UnpaidAbsenceQuantity), summary);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0002")]
  public void No_pay_element_behaviour_exists_without_an_input_this_product_has()
  {
    // ---- THE PRINCIPLE, PRESERVED AND STATED POSITIVELY.
    //
    // `DEC-PAY-0002` was never "no overtime"; it was **no behaviour whose input does not exist**. Attendance
    // now supplies hours, tiers and unpaid days — so overtime and absence deduction are permitted, and
    // SHIFT DIFFERENTIAL and LATENESS are still not, because `AttendanceRecord` records neither.
    //
    // `DEC-PAY-0016` independently bars tax and statutory brackets: V1 is jurisdiction-neutral, and
    // Attendance did nothing to change that.
    Assert.DoesNotContain(
      Enum.GetNames<PayElementBehaviour>(),
      name => name.Contains("Shift", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Differential", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Late", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0022")]
  public void Payroll_reaches_attendance_only_through_the_published_contract()
  {
    // `DEC-ATT-0002` and `ADR-012`. The contracts assembly is loaded BY REFERENCE — via a type — rather than
    // by name, so a rename cannot make this guard silently vacuous. That is the failure mode FP-012 found in
    // GL's original absence guard.
    var contracts = typeof(IAttendanceSummary).Assembly.GetName().Name;
    Assert.Equal("SSAS.Attendance.Contracts", contracts);

    var payrollReferences = typeof(PayrollCalculator).Assembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name)
      .ToArray();

    // The DOMAIN does not reference the contracts at all: the handler unpacks the contract into plain values
    // before the calculator sees them, so `ADR-012`'s boundary is kept at the layer where it costs nothing.
    Assert.DoesNotContain(payrollReferences, name =>
      name is not null && name.StartsWith("SSAS.Attendance", StringComparison.Ordinal));
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
