using SSAS.BuildingBlocks.SharedKernel;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests.Runs;

// ================================================================================================
// THE ARITHMETIC OF THE TWO ATTENDANCE-DRIVEN BEHAVIOURS (FP-013, REQ-ATT-0022).
// ================================================================================================
//
// `DEC-PAY-0002` refused these because the INPUT did not exist. FP-013 built it, and this is the proof that
// what arrives through `IAttendanceSummary` turns into the right money.
//
// The calculator takes plain values rather than the contract type, which is why every case here reads as
// arithmetic with a dictionary literal instead of a constructed contract record.
public sealed class AttendanceDrivenCalculationTests
{
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid SalaryAccount = Guid.NewGuid();
  private static readonly Guid OvertimeAccount = Guid.NewGuid();
  private static readonly Guid AbsenceAccount = Guid.NewGuid();

  // January 2026: 31 calendar days, which is the divisor `UnpaidAbsenceDeduction` uses.
  private static PayrollPeriod Period() => PayrollTestData.Period();

  private static EmployeeCompensation Compensation(decimal baseAmount, params (Guid Element, decimal? Rate)[] assignments) =>
    PayrollTestData.Compensation(
      Employee,
      new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
      baseAmount,
      assignments.Select(assignment => (assignment.Element, assignment.Rate)).ToArray());

  private static PayrollEmployeeInput Input(
    EmployeeCompensation compensation,
    IReadOnlyDictionary<string, decimal>? overtime = null,
    decimal unpaidAbsence = 0m) =>
    new(Employee,
      new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), null, compensation,
      overtime, unpaidAbsence);

  // ---- OVERTIME: QUANTITY FROM ATTENDANCE, RATE FROM PAYROLL (OD-ATT-0008).
  [Fact]
  [Trait("Requirement", "REQ-ATT-0022")]
  public void Overtime_multiplies_the_attendance_quantity_by_the_elements_rate()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element(
      "OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    Assert.True(night.SetOvertimeTier("NIGHT").IsSuccess);

    var compensation = Compensation(3100m, (night.Id, null));
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal) { ["NIGHT"] = 6m });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night]);

    Assert.True(lines.IsSuccess);
    var overtimeLine = Assert.Single(lines.Value.Where(line => line.PayElementId == night.Id));
    Assert.Equal(150m, overtimeLine.Amount);
  }

  // ================================================================================================
  // THE TIER IS MATCHED ON A NORMALIZED KEY, NOT ON WHAT THE OPERATOR TYPED (T-131).
  // ================================================================================================
  //
  // **Before T-131 this paid nothing and said nothing.** The record's tier and the element's tier are
  // written in different modules by different people; neither side case-folded, and the lookup is ordinal,
  // **so `"Night"` against `"NIGHT"` missed and `OvertimeQuantity` returned `0m`** — no error, no warning,
  // and a payslip that looked complete.
  //
  // **The quantity keys here deliberately use the CASE AN OPERATOR WOULD TYPE rather than the normalized
  // form**, because a test that pre-normalizes its own inputs proves only that the dictionary works.
  [Theory]
  [InlineData("NIGHT", "NIGHT")]
  [InlineData("Night", "NIGHT")]
  [InlineData("NIGHT", "night")]
  [InlineData("  Night  ", "night")]
  public void A_tier_matches_its_element_regardless_of_how_either_side_was_typed(
    string recordedTier, string elementTier)
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element(
      "OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    Assert.True(night.SetOvertimeTier(elementTier).IsSuccess);

    var compensation = Compensation(3100m, (night.Id, null));
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
      [OvertimeTierKey.Normalize(recordedTier)!] = 6m
    });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night]);

    Assert.True(lines.IsSuccess);
    var overtimeLine = Assert.Single(lines.Value.Where(line => line.PayElementId == night.Id));
    Assert.Equal(150m, overtimeLine.Amount);
  }

  // ================================================================================================
  // OVERTIME WORKED AGAINST A TIER NOTHING PRICES REFUSES THE RUN (T-149).
  // ================================================================================================
  //
  // **Before T-149 this paid zero and said nothing.** The hours existed, no element named the tier, and
  // `OvertimeQuantity` returned `0m` — indistinguishable from an employee who worked no overtime, on a
  // payslip that looked complete.
  //
  // Refused on the precedent this module set twice: `DailySalaryHasNoWorkingDays` and
  // `AttendanceContradictsEmployment` both refuse rather than producing a defensible-looking zero.
  [Fact]
  [Trait("Decision", "OD-ATT-0008")]
  public void Overtime_under_a_tier_no_assigned_element_prices_refuses_the_run()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element(
      "OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    Assert.True(night.SetOvertimeTier("NIGHT").IsSuccess);

    var compensation = Compensation(3100m, (night.Id, null));

    // Worked a HOLIDAY tier; the only element they hold prices NIGHT.
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
      ["NIGHT"] = 6m,
      ["HOLIDAY"] = 3m
    });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night]);

    Assert.True(lines.IsFailure);

    // The SPECIFIC refusal — a daily-salary or employment-contradiction failure would also be a failure.
    Assert.Equal(PayrollErrors.OvertimeTierHasNoPricedElement, lines.Error);
  }

  // ---- ⚠ AND IT MUST NOT FIRE FOR AN EMPLOYEE WHO IS SIMPLY NOT PAID OVERTIME.
  //
  // The calculator's assignment rule states that an unassigned `OvertimeHourly` element MEANS "this employee
  // is not paid overtime" — **a legitimate standing instruction.** Attendance may still record their hours;
  // payroll simply does not price them.
  //
  // **Refusing here would break a supported configuration**, which is why the guard requires at least one
  // assigned overtime element before it fires. This is the test that holds that line.
  [Fact]
  [Trait("Decision", "OD-ATT-0008")]
  public void Overtime_hours_for_an_employee_with_no_overtime_element_are_not_a_refusal()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element(
      "OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    night.SetOvertimeTier("NIGHT");

    // The element EXISTS in the run but is NOT assigned to this employee.
    var compensation = Compensation(3100m);
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
      ["NIGHT"] = 6m
    });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night]);

    Assert.True(lines.IsSuccess, lines.IsFailure ? lines.Error.Code : null);

    // And no overtime line — absent, not zero.
    Assert.DoesNotContain(lines.Value, line => line.PayElementId == night.Id);
  }

  // Each element prices ONE tier. A company defines one per tier, and an element whose tier is absent from
  // the summary contributes nothing rather than silently pricing somebody else's hours.
  [Fact]
  [Trait("Decision", "OD-ATT-0008")]
  public void Each_overtime_element_prices_only_its_own_tier()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element("OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    night.SetOvertimeTier("NIGHT");
    var holiday = PayrollTestData.Element("OT-HOL", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 40m, 11, OvertimeAccount);
    holiday.SetOvertimeTier("HOLIDAY");

    var compensation = Compensation(3100m, (night.Id, null), (holiday.Id, null));
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
      ["NIGHT"] = 6m,
      ["HOLIDAY"] = 2m
    });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night, holiday]);

    Assert.Equal(150m, lines.Value.Single(line => line.PayElementId == night.Id).Amount);
    Assert.Equal(80m, lines.Value.Single(line => line.PayElementId == holiday.Id).Amount);
  }

  // An element whose tier no attendance names produces NO LINE — not a zero one. Absent and zero are
  // different facts, and a zero line would clutter every payslip with things that did not happen.
  //
  // ---- ⚠ ITS DATA CHANGED IN T-149, AND THE REASON MATTERS MORE THAN THE CHANGE.
  //
  // This test previously gave the employee a NIGHT element and HOLIDAY hours — **which is now a
  // refusal**, because those HOLIDAY hours were priced by nothing. The old data proved the stated
  // intent AND silently encoded the misconfiguration `OvertimeTierHasNoPricedElement` exists to catch.
  //
  // **The intent is unchanged and is what this still tests**: the employee now holds BOTH elements and
  // works only HOLIDAY, so NIGHT is an element whose tier was never worked — and every worked tier is
  // priced. **The case removed from here is asserted in
  // `Overtime_under_a_tier_no_assigned_element_prices_refuses_the_run` with the opposite outcome**, so
  // coverage increased rather than moved.
  [Fact]
  public void An_overtime_element_whose_tier_was_never_worked_produces_no_line()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element("OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    night.SetOvertimeTier("NIGHT");
    var holiday = PayrollTestData.Element("OT-HOL", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 40m, 11, OvertimeAccount);
    holiday.SetOvertimeTier("HOLIDAY");

    var compensation = Compensation(3100m, (night.Id, null), (holiday.Id, null));
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal) { ["HOLIDAY"] = 5m });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night, holiday]);

    Assert.True(lines.IsSuccess, lines.IsFailure ? lines.Error.Code : null);

    // NIGHT was never worked: no line at all.
    Assert.DoesNotContain(lines.Value, line => line.PayElementId == night.Id);

    // And HOLIDAY was, so it is priced — which is what makes this a legal run rather than a refusal.
    Assert.Equal(200m, lines.Value.Single(line => line.PayElementId == holiday.Id).Amount);
  }

  // ---- OVERTIME IS NOT PRORATED, AND THAT IS DELIBERATE.
  //
  // Overtime is hours ACTUALLY WORKED, not an entitlement spread across a period. A mid-month joiner who
  // worked six overtime hours worked six; scaling them by their fraction of the month would pay them for
  // fewer hours than they were present for.
  [Fact]
  public void Overtime_is_not_prorated_for_a_mid_period_joiner()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element("OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    night.SetOvertimeTier("NIGHT");

    var compensation = Compensation(3100m, (night.Id, null));

    // Hired on the 17th: roughly half the period, so base salary IS prorated.
    var joiner = new PayrollEmployeeInput(
      Employee, new DateTimeOffset(2026, 1, 17, 0, 0, 0, TimeSpan.Zero), null, compensation,
      new Dictionary<string, decimal>(StringComparer.Ordinal) { ["NIGHT"] = 6m }, 0m);

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [joiner], [basic, night]);

    var baseLine = lines.Value.Single(line => line.PayElementId == basic.Id);
    var overtimeLine = lines.Value.Single(line => line.PayElementId == night.Id);

    // Base is prorated; overtime is not.
    Assert.True(baseLine.Amount < 3100m);
    Assert.Equal(150m, overtimeLine.Amount);
  }

  // ================================================================================================
  // UNPAID ABSENCE: THE DAILY RATE USES THE CALENDAR-DAY DIVISOR (OD-ATT-0015).
  // ================================================================================================
  //
  // `OD-ATT-0015` asked whether building a working calendar reopened `OD-PAY-0007`, and the owner ruled
  // **proration UNCHANGED — calendar days**. So this deduction divides by the period's calendar days, and
  // NOT by its working days.
  //
  // Using working days here would make a day of absence and a day of proration worth different amounts,
  // which is exactly the inconsistency that ruling prevents. 3100 / 31 = 100 per day.
  [Fact]
  [Trait("Decision", "OD-ATT-0015")]
  public void Unpaid_absence_deducts_the_calendar_day_rate_times_the_days_absent()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    var compensation = Compensation(3100m);
    var input = Input(compensation, unpaidAbsence: 2m);

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, absence]);

    var deduction = Assert.Single(lines.Value.Where(line => line.PayElementId == absence.Id));

    // 3100 / 31 calendar days = 100/day; two days absent = 200.
    Assert.Equal(200m, deduction.Amount);

    // POSITIVE, like every amount in this module. `Kind` is what makes it deduct — encoding the distinction
    // as a sign is what `PayElementKind`'s own comment refuses.
    Assert.Equal(PayElementKind.Deduction, deduction.Kind);
    Assert.True(deduction.Amount > 0m);
  }

  // ================================================================================================
  // THE EXEMPTION THAT PREVENTS A SILENT OVERPAYMENT.
  // ================================================================================================
  //
  // `UnpaidAbsenceDeduction` needs no per-employee assignment. If it did, **an employee nobody remembered to
  // assign it to would have their unpaid leave silently go undeducted** — paid in full for days they did not
  // work, with every number on the payslip looking right.
  //
  // This is the single most valuable test in this file, because the failure it guards is invisible.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0022")]
  public void Unpaid_absence_deducts_even_when_the_employee_has_no_assignment_for_it()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    // NO assignment for the absence element at all.
    var compensation = Compensation(3100m);
    var input = Input(compensation, unpaidAbsence: 1m);

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, absence]);

    Assert.Contains(lines.Value, line => line.PayElementId == absence.Id);
  }

  // The opposite decision for overtime, and its reason: eligibility for overtime pay is a real per-employee
  // decision, and the failure direction is a MISSING payment somebody notices rather than a silent
  // overpayment nobody does.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0022")]
  public void Overtime_requires_an_assignment_because_eligibility_is_a_real_decision()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var night = PayrollTestData.Element("OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, 25m, 10, OvertimeAccount);
    night.SetOvertimeTier("NIGHT");

    // No assignment for the overtime element.
    var compensation = Compensation(3100m);
    var input = Input(compensation, overtime: new Dictionary<string, decimal>(StringComparer.Ordinal) { ["NIGHT"] = 6m });

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, night]);

    Assert.DoesNotContain(lines.Value, line => line.PayElementId == night.Id);
  }

  // An employee with NO attendance at all — the ordinary case for a company that configures no
  // attendance-driven elements — calculates exactly as it did before FP-013.
  [Fact]
  public void An_employee_with_no_attendance_calculates_as_before()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var absence = PayrollTestData.Element(
      "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccount);

    var compensation = Compensation(3100m);

    // The defaulted constructor — no overtime dictionary, no unpaid days.
    var input = new PayrollEmployeeInput(
      Employee, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), null, compensation);

    var lines = PayrollCalculator.Calculate(Guid.NewGuid(), Period(), [input], [basic, absence]);

    // The base line, and no deduction: a zero-valued line is skipped rather than stored.
    Assert.Equal(3100m, lines.Value.Single(line => line.PayElementId == basic.Id).Amount);
    Assert.DoesNotContain(lines.Value, line => line.PayElementId == absence.Id);
  }

  // ---- THE TIER IS ONLY MEANINGFUL ON AN OVERTIME ELEMENT.
  //
  // A tier on any other behaviour would sit in the database looking like configuration while the calculator
  // never consults it — so it is refused rather than ignored.
  [Fact]
  public void A_tier_on_a_non_overtime_element_is_refused()
  {
    var basic = PayrollTestData.Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var set = basic.SetOvertimeTier("NIGHT");

    Assert.True(set.IsFailure);
    Assert.Equal(PayElementErrors.OvertimeTierNotApplicable.Code, set.Error.Code);
  }
}
