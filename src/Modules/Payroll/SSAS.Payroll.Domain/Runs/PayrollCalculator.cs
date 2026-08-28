using SSAS.BuildingBlocks.Domain;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;

namespace SSAS.Payroll.Domain.Runs;
// What the calculator needs to know about one employee. Assembled by the handler from HR data, this module's
// own compensation history, and — since FP-013 — Attendance's per-period summary. The calculator itself
// touches no repository and no clock, which is what makes `TS-PAY-0010` and the determinism scenarios
// assertable without a database.
public sealed record PayrollEmployeeInput(
  Guid EmployeeId,
  DateTimeOffset HiredUtc,
  DateTimeOffset? TerminatedUtc,
  EmployeeCompensation Compensation,

  // ---- THE ATTENDANCE INPUT (FP-013, REQ-ATT-0022) — PLAIN VALUES, NOT THE CONTRACT TYPE.
  //
  // `AttendanceSummaryResult` lives in `SSAS.Attendance.Contracts`, and this is `SSAS.Payroll.Domain`.
  // The handler unpacks the contract into these two fields, so the domain gains the capability without the
  // reference — `ADR-012`'s boundary applied at the layer where it costs nothing to keep.
  //
  // It also keeps the calculator testable with a dictionary literal rather than a constructed contract
  // record, which is why every attendance scenario in `PayrollCalculatorTests` reads as arithmetic.
  //
  // Both DEFAULT TO EMPTY. An employee with no attendance is the ordinary case for a company that configures
  // no attendance-driven elements, and it must not require every existing call site to say so.
  IReadOnlyDictionary<string, decimal>? OvertimeQuantityByTier = null,

  // ---- THE UNITS, STATED ONCE AND ASSERTED BY TEST (T-107).
  //
  // **`UnpaidAbsenceQuantity` is DAYS. `OvertimeQuantityByTier` and `WorkedQuantity` are HOURS.**
  //
  // Two quantities from the SAME attendance record are consumed in DIFFERENT units, and before T-107 both
  // units lived in prose comments and were asserted nowhere — no validation, no test, no schema constraint.
  // An hourly rate multiplied by a day count is a payroll defect nobody would see, so
  // `PayrollCalculatorTests` now asserts each unit through arithmetic that fails if the other is assumed.
  decimal UnpaidAbsenceQuantity = 0m,

  // HOURS. Read only by `SalaryType.Hourly`; `WorkedQuantity` had NO consumer at all before T-107, which is
  // why its unit could be established here rather than inherited.
  decimal WorkedQuantity = 0m,

  // DAYS, and a COMPANY-AND-PERIOD fact rather than an employee one — what the company's calendar says this
  // period contains, before anything about this employee. Read only by `SalaryType.Daily` (T-108).
  int StandardWorkingDays = 0);

// ================================================================================================
// THE CALCULATION ENGINE (OD-PAY-0007, OD-PAY-0008).
// ================================================================================================
//
// Deterministic and pure: same inputs, same lines, every time. No `DateTimeOffset.UtcNow`, no repository, no
// ambient state. A calculation that varied with the clock could not be reproduced, and `BR-PLT-0103` treats
// payroll processing as sensitive precisely because it must be answerable after the fact.
//
// ---- THE THREE RULED BEHAVIOURS, IN ONE PLACE.
//
// **Ordering (`OD-PAY-0007`).** Elements evaluate in ascending `CalculationOrder`. The tie-break is the
// element's normalized code, because two elements may legitimately share an ordinal when neither depends on
// the other — and without a total order the result would depend on row order, which is a difference nobody
// can see in a diff and everybody can see in a payslip.
//
// **Proration (`OD-PAY-0007`).** CALENDAR DAYS. A mid-period joiner or leaver is paid
// `amount * employedDays / periodDays`. Working-day proration was refused because it needs a working
// calendar the product does not have — that is Attendance, and `DEC-PAY-0002` bars deriving anything from
// input that does not exist.
//
// **Rounding (`OD-PAY-0008`).** Each line is rounded to 2dp, **half away from zero**, and a run total is the
// SUM OF ROUNDED LINES. The protected invariant is that **the payslip adds up** — the first property a human
// checks. Rounding only the net total would make the printed lines fail to sum to it, which reads as a bug
// forever. Note the interaction with `ADR-027`: `decimal(19,4)` is the STORAGE precision and says nothing
// about what a person is paid.
//
// ---- WHAT IS NOT HERE, AND WHY IT CANNOT BE.
//
// No tax, no statutory contribution, no bracket, no filing (`DEC-PAY-0016`). V1 is jurisdiction-neutral, so
// the net figure below is **gross minus configured deductions** and is not a legally compliant net pay in any
// jurisdiction. A tenant may configure a deduction; the product will not compute a liability.
//
// No overtime, absence deduction, shift differential or lateness (`DEC-PAY-0002`) — no attendance register
// exists to derive them from.
public static class PayrollCalculator
{
  // Two decimal places, half away from zero. Named rather than inlined so every site rounds identically and
  // a reader can find the one place the rule lives.
  public static decimal RoundLine(decimal amount) =>
    Math.Round(amount, 2, MidpointRounding.AwayFromZero);

  public static Result<IReadOnlyList<PayrollRunDraftLine>> Calculate(
    Guid payrollRunId,
    PayrollPeriod period,
    IReadOnlyList<PayrollEmployeeInput> employees,
    IReadOnlyList<PayElement> elements)
  {
    ArgumentNullException.ThrowIfNull(period);
    ArgumentNullException.ThrowIfNull(employees);
    ArgumentNullException.ThrowIfNull(elements);

    // Inclusion is the period's rule, not the calculator's — `PayrollPeriod.Includes` carries the
    // `BR-HR-0004` reading `OD-PAY-0010` ruled, and duplicating it here would create a second answer to
    // "who gets paid" that could drift from the first.
    var included = employees
      .Where(employee => period.Includes(employee.HiredUtc, employee.TerminatedUtc))
      .OrderBy(employee => employee.EmployeeId)
      .ToList();

    if (included.Count == 0)
    {
      return Result.Failure<IReadOnlyList<PayrollRunDraftLine>>(PayrollErrors.NoIncludedEmployees);
    }

    // Ascending order, then normalized code as a total-order tie-break. Inactive elements are excluded here
    // rather than refused: deactivating an element is how a company stops using it, and a run that refused to
    // calculate because a no-longer-used element exists would be unusable.
    var ordered = elements
      .Where(element => element.IsActive)
      // The net-pay payable carries a MAPPING, not an amount. Net pay is derived from the other lines, so a
      // line for it would double-count -- see the behaviour's own comment.
      .Where(element => element.Behaviour != PayElementBehaviour.NetPayPayable)
      .OrderBy(element => element.CalculationOrder)
      .ThenBy(element => element.NormalizedCode, StringComparer.Ordinal)
      .ToList();

    var periodDays = (decimal)(period.EndUtc.Date - period.StartUtc.Date).TotalDays + 1m;
    if (periodDays <= 0m)
    {
      return Result.Failure<IReadOnlyList<PayrollRunDraftLine>>(PayrollErrors.PeriodBoundsInvalid);
    }

    var lines = new List<PayrollRunDraftLine>();

    foreach (var employee in included)
    {
      var factor = ProrationFactor(period, employee, periodDays);

      // Base salary is prorated then rounded ONCE, here, and that single figure is both the `BaseSalary`
      // element's line amount and the base every percentage behaviour is computed from. Computing
      // percentages off the unrounded base would produce lines that do not agree with the base line the
      // employee can see — the payslip would be internally inconsistent while every number was defensible.
      // ---- WHAT THE BASE AMOUNT MEANS DEPENDS ON THE SALARY TYPE (T-107, OD-PAY-0015).
      //
      // **The `Monthly` arm is the pre-T-107 expression VERBATIM**, so every existing calculation is
      // unchanged by construction rather than by measurement — `SalaryType.Monthly` is `default`, so every
      // record written before this change takes exactly the path it always took.
      //
      // **`Daily` and `Hourly` are NOT prorated, and that is the whole point.** Proration answers "how much
      // of the period was this employee employed for"; a rate times a worked quantity has ALREADY answered
      // it. Multiplying again pays a mid-month joiner a fraction of what they actually earned. `OD-PAY-0015`
      // ruled proration for the monthly model and it must not silently extend to a model it was not about —
      // which is the same reasoning `OvertimeHourly` already applies two screens below.
      //
      // A daily salary with no working days to price cannot be calculated, and the run refuses rather than
      // paying zero. See `PayrollErrors.DailySalaryHasNoWorkingDays` for why the error names what was
      // observed rather than which of its three causes occurred.
      if (employee.Compensation.SalaryType == SalaryType.Daily && employee.StandardWorkingDays <= 0)
      {
        return Result.Failure<IReadOnlyList<PayrollRunDraftLine>>(
          PayrollErrors.DailySalaryHasNoWorkingDays);
      }

      var baseAmount = RoundLine(employee.Compensation.SalaryType switch
      {
        SalaryType.Hourly => employee.Compensation.BaseAmount * employee.WorkedQuantity,

        // ---- DAILY IS THE STANDARD WORKING DAYS, LESS THE UNPAID ONES (T-108, owner-ruled).
        //
        // **AND IT IS NOT `rate x days ACTUALLY WORKED`**, which is the reading the phrase *"daily rate"*
        // invites and the one a reader will assume. Under that model absence is already excluded and the
        // `UnpaidAbsenceDeduction` element would double-count it — the same error the hourly path avoids by
        // taking no deduction at all.
        //
        // The owner excluded HOURLY from that deduction *because an hourly employee is paid only for the
        // time they attend*, and included DAILY. **That contrast only means something if a daily employee
        // is NOT paid solely for time attended** — so a daily employee is paid the period's working days
        // and the deduction takes the unpaid ones back.
        //
        // Clamped at zero: more unpaid days than the period holds is bad data, and a NEGATIVE base would
        // flow into every percentage element and produce a payslip that looks arithmetically consistent
        // while being nonsense.
        SalaryType.Daily => employee.Compensation.BaseAmount * Math.Max(
          0m, employee.StandardWorkingDays - employee.UnpaidAbsenceQuantity),

        // MONTHLY AND `default` TAKE THE SAME ARM, AND IT IS THE PRE-T-107 EXPRESSION VERBATIM.
        _ => employee.Compensation.BaseAmount * factor
      });

      var sequence = 0;
      var grossToDate = 0m;

      foreach (var element in ordered)
      {
        var assignment = employee.Compensation.Assignments
          .FirstOrDefault(a => a.PayElementId == element.Id);

        // ---- BASE SALARY NEEDS NO ASSIGNMENT; EVERYTHING ELSE DOES.
        //
        // A `BaseSalary` element draws from the compensation record itself, so requiring a per-employee
        // assignment for it would be ceremony. Every other behaviour is a standing instruction that must
        // have been granted to this employee.
        //
        // ---- AND SINCE FP-013, `UnpaidAbsenceDeduction` IS EXEMPT TOO — FOR A DIFFERENT AND SHARPER REASON.
        //
        // Its amount is DERIVED from base salary and the days Attendance reported, so like base salary it
        // needs no per-employee configuration. But the consequence of getting this wrong is not ceremony:
        // **an employee nobody remembered to assign the element to would have their unpaid leave silently
        // go undeducted.** They would simply be paid in full for days they were not working, and every
        // number on the payslip would look right.
        //
        // `OvertimeHourly` is deliberately NOT exempt. Overtime eligibility is a real per-employee decision,
        // and its absence means "this employee is not paid overtime" — a legitimate standing instruction,
        // and the failure direction is a missing payment somebody notices rather than a silent overpayment
        // nobody does.
        //
        // An element the employee is not assigned produces NO LINE AT ALL — not a zero line. A zero line
        // would clutter every payslip with things that do not apply, and "absent" and "zero" are different
        // facts.
        if (assignment is null &&
          element.Behaviour is not (PayElementBehaviour.BaseSalary or PayElementBehaviour.UnpaidAbsenceDeduction))
        {
          continue;
        }

        var rateOrAmount = assignment?.RateOrAmount ?? element.DefaultRateOrAmount;

        var raw = element.Behaviour switch
        {
          // The employee's own base amount, prorated. Note it uses `baseAmount`, which is already prorated
          // and rounded, so the base line and every percentage computed from it agree exactly.
          PayElementBehaviour.BaseSalary => baseAmount,

          // A fixed amount is prorated: a mid-month joiner receives a part-month allowance, which is the
          // same treatment their salary gets.
          PayElementBehaviour.FixedAmount => rateOrAmount * factor,
          PayElementBehaviour.PercentageOfBaseSalary => baseAmount * rateOrAmount / 100m,
          PayElementBehaviour.PercentageOfGrossToDate => grossToDate * rateOrAmount / 100m,

          // ---- OVERTIME AT A TIER (FP-013, OD-ATT-0008).
          //
          // Attendance supplied the QUANTITY per tier; this element supplies the RATE for the one tier it
          // names. An element whose tier is absent from the summary contributes zero and is skipped below,
          // on the same rule as any other zero line.
          //
          // NOT PRORATED, and that is deliberate. Overtime is hours actually worked, not an entitlement
          // spread across a period: a mid-month joiner who worked six overtime hours worked six, and scaling
          // them by their fraction of the month would pay them for fewer hours than they were present for.
          PayElementBehaviour.OvertimeHourly => OvertimeQuantity(employee, element) * rateOrAmount,

          // ---- UNPAID ABSENCE (FP-013, OD-ATT-0008).
          //
          // Daily rate times unpaid days, where the daily rate divides the PRORATED base by the period's
          // CALENDAR days — the same divisor proration itself uses, because `OD-ATT-0015` ruled `OD-PAY-0007`
          // UNCHANGED. Using working days here would make a day of absence and a day of proration worth
          // different amounts, which is exactly the inconsistency that ruling prevents.
          //
          // The amount is POSITIVE like every other amount in this module; the element's `Kind` is what makes
          // it deduct. A negative here would encode the distinction as a sign, which `PayElementKind`'s own
          // comment refuses.
          //
          // ---- AND IT NEVER APPLIES TO AN HOURLY SALARY (T-107, owner-ruled).
          //
          // **An hourly employee is paid only for the time they attend**, so the worked quantity has ALREADY
          // accounted for the absence: deducting again charges them twice for one absence. Worse, the
          // divisor here is the period's CALENDAR days, which against an hourly rate is not merely
          // redundant but meaningless.
          //
          // **`Daily` DOES take it.** A daily rate times days worked and a deduction for unpaid days both
          // count in DAYS, so the arithmetic is coherent — and the owner ruled it applies.
          //
          // Zero rather than "no line": the element is exempt from requiring an assignment, so a company
          // that configures it gets a zero contribution which the zero-line rule below then suppresses.
          PayElementBehaviour.UnpaidAbsenceDeduction =>
            employee.Compensation.SalaryType is SalaryType.Monthly or SalaryType.Daily
              ? baseAmount / periodDays * employee.UnpaidAbsenceQuantity
              : 0m,

          _ => 0m
        };

        var amount = RoundLine(raw);

        // A zero-valued line is skipped rather than stored. It carries no information and would appear on a
        // payslip as a line item for nothing.
        if (amount == 0m)
        {
          continue;
        }

        lines.Add(new PayrollRunDraftLine(
          Guid.NewGuid(), payrollRunId, employee.EmployeeId, element.Id, element.Kind,
          amount, sequence++, element.GlAccountId));

        // Only EARNINGS accumulate into the base for `PercentageOfGrossToDate`. A deduction reducing the base
        // of a later deduction would make the order of two deductions change the total, which is the kind of
        // dependency nobody intends and everybody discovers late.
        if (element.Kind == PayElementKind.Earning)
        {
          grossToDate += amount;
        }
      }
    }

    return Result.Success<IReadOnlyList<PayrollRunDraftLine>>(lines);
  }

  // The quantity Attendance reported for the tier this element prices. Absent tier, or no attendance at all,
  // is zero — which produces no line rather than a zero one.
  //
  // Ordinal comparison, matching how `AttendanceSummaryService` groups them. A culture-sensitive lookup
  // would make the same tier label match or not depending on the server's locale.
  private static decimal OvertimeQuantity(PayrollEmployeeInput employee, PayElement element)
  {
    if (element.OvertimeTier is not { } tier || employee.OvertimeQuantityByTier is not { } quantities)
    {
      return 0m;
    }

    return quantities.TryGetValue(tier, out var quantity) ? quantity : 0m;
  }

  // CALENDAR-DAY PRORATION. Employed days over period days, capped at 1.
  //
  // Both boundaries are inclusive: someone hired on the last day of the period is paid for one day, and
  // someone terminated on the first day is paid for one day. `TS-PAY-0010` asserts both ends, because
  // off-by-one at a boundary is the defect this method exists to get right.
  private static decimal ProrationFactor(
    PayrollPeriod period,
    PayrollEmployeeInput employee,
    decimal periodDays)
  {
    var from = employee.HiredUtc.ToUniversalTime().Date > period.StartUtc.Date
      ? employee.HiredUtc.ToUniversalTime().Date
      : period.StartUtc.Date;

    var to = period.EndUtc.Date;
    if (employee.TerminatedUtc is { } terminated && terminated.ToUniversalTime().Date < to)
    {
      to = terminated.ToUniversalTime().Date;
    }

    if (to < from)
    {
      return 0m;
    }

    var employedDays = (decimal)(to - from).TotalDays + 1m;
    return employedDays >= periodDays ? 1m : employedDays / periodDays;
  }
}
