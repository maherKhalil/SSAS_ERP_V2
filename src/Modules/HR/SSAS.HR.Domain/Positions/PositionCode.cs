using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE POSITION'S BUSINESS IDENTIFIER (REQ-HR-0200, DEC-POS-0007, DEC-POS-0024).
//
// USER-ENTERED, never generated. `BR-PLT-0006` lists per-company configurable numbering sequences, but no
// numbering mechanism exists in the platform; FP-006 declined to build one for `EmployeeNumber` and FP-007
// declined again for `DepartmentCode`. This is the third refusal, not a new question.
//
// UNIQUE WITHIN A COMPANY, and per company is the whole scope: `OD-POS-003` ruled Position independent of
// Department, so there is no per-department variant to build.
//
// The length is 32, per `data-model.md`. `DepartmentCode` is 64 and `EmployeeNumber` is its own; the limit
// is per specification rather than per convention, so it is read from the package rather than copied.
public sealed class PositionCode : ValueObject
{
  public const int MaximumLength = 32;

  private PositionCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // The trimmed value with display casing preserved.
  public string Value { get; }

  // Exactly Trim().ToUpperInvariant(); the column carrying it is binary-collated so comparison is ordinal.
  public string NormalizedValue { get; }

  public static Result<PositionCode> Create(string? value) =>
    OrganizationalText.TryNormalizeCode(value, MaximumLength, out var trimmed, out var normalized)
      ? Result.Success(new PositionCode(trimmed, normalized))
      : Result.Failure<PositionCode>(PositionErrors.InvalidCode);

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}
