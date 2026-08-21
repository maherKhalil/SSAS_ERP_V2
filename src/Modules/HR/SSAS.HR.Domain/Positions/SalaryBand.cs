using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE SALARY GRADE'S PAY BAND — ATOMIC (REQ-HR-0202, DEC-POS-0016, DEC-POS-0027).
//
// ================================================================================================
// ALL THREE AMOUNTS, OR NONE. THERE IS NO PARTIALLY PRICED BAND.
// ================================================================================================
//
// A band is either defined or it is not, and the model refuses to represent anything between. A grade with
// a minimum and no maximum is not a half-answer — it is a row nobody can act on, and it would force every
// reader downstream to invent a rule for what a missing ceiling means. `DEC-POS-0027` rules the band atomic,
// and this type is where that ruling lives: the three amounts are non-nullable FIELDS OF ONE VALUE, and the
// value itself is what may be absent.
//
// The persistence side mirrors it exactly. `SalaryGrade.Band` is an OPTIONAL OWNED type, so all three
// columns are null together or populated together, and `CK_SalaryGrades_Band_Atomic` states the same rule to
// SQL Server for writes that bypass the application entirely.
//
// ---- WHY NULLABLE AT ALL, NOW THAT THE ORIGINAL REASON IS GONE.
//
// The draft justified nullability with the `OD-POS-001` backfill: a seeded `UNASSIGNED` grade would have had
// to invent a minimum and a maximum nobody had chosen. **That reason died with the ruling** — no grade is
// seeded, so nothing has to be invented. What remains is DEFINE-BEFORE-PRICE: a job ladder is commonly laid
// out before it is benchmarked, and a grade awaiting benchmarking has no honest amounts. That is a real case
// but a smaller one, and `DEC-POS-0016` records it as an open question rather than pretending the original
// justification still stands.
//
// ---- WHAT THE BAND DOES NOT DO.
//
// It constrains nothing outside its own row. `OD-POS-004` chose INFORMATIONAL bands, and FP-008 stores no
// employee compensation for a range to validate (`DEC-POS-0023`). Range ENFORCEMENT transfers to Payroll and
// is recorded as transferred, not as realized — writing a validator here would mark a rule as enforced when
// no write in the product can violate it, which is the failure `ADR-026` decision 10 names.
public sealed class SalaryBand : ValueObject
{
  private SalaryBand(decimal minimumAmount, decimal midpointAmount, decimal maximumAmount)
  {
    MinimumAmount = minimumAmount;
    MidpointAmount = midpointAmount;
    MaximumAmount = maximumAmount;
  }

  // EF materialization only. The parameterless constructor an owned type requires.
  private SalaryBand()
  {
  }

  public decimal MinimumAmount { get; private set; }

  public decimal MidpointAmount { get; private set; }

  public decimal MaximumAmount { get; private set; }

  // ---- CONSTRUCTION.
  //
  // Returns `Result<SalaryBand?>` rather than `Result<SalaryBand>` because ABSENCE IS A LEGAL ANSWER and it
  // is not an error. All three null yields a successful null; any other partial combination is refused. A
  // caller that could not distinguish "unpriced" from "invalid" would have to guess, and the guess would be
  // wrong in exactly the case that matters.
  public static Result<SalaryBand?> Create(
    decimal? minimumAmount, decimal? midpointAmount, decimal? maximumAmount)
  {
    var present =
      (minimumAmount.HasValue ? 1 : 0) +
      (midpointAmount.HasValue ? 1 : 0) +
      (maximumAmount.HasValue ? 1 : 0);

    if (present == 0)
    {
      return Result.Success<SalaryBand?>(null);
    }

    // ONE OR TWO AMOUNTS IS THE REFUSED CASE, and it is refused before the ordering is examined: a partial
    // band has no ordering to be right or wrong about, so answering with an ordering error would name the
    // wrong defect.
    if (present != 3)
    {
      return Result.Failure<SalaryBand?>(PositionErrors.SalaryBandIncomplete);
    }

    var minimum = minimumAmount!.Value;
    var midpoint = midpointAmount!.Value;
    var maximum = maximumAmount!.Value;

    if (minimum < 0m || midpoint < 0m || maximum < 0m)
    {
      return Result.Failure<SalaryBand?>(PositionErrors.SalaryBandNegative);
    }

    // NON-STRICT. A band whose three amounts are equal is a single-point band, which is a real structure —
    // a fixed-rate grade — and refusing it would be a rule no requirement asks for.
    if (minimum > midpoint || midpoint > maximum)
    {
      return Result.Failure<SalaryBand?>(PositionErrors.SalaryBandOutOfOrder);
    }

    return Result.Success<SalaryBand?>(new SalaryBand(minimum, midpoint, maximum));
  }

  public override string ToString() =>
    $"{MinimumAmount} / {MidpointAmount} / {MaximumAmount}";

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return MinimumAmount;
    yield return MidpointAmount;
    yield return MaximumAmount;
  }
}
