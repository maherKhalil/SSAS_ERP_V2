using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE SALARY GRADE'S BUSINESS IDENTIFIER (REQ-HR-0202, DEC-POS-0007).
//
// Distinct from `JobGradeCode` for the reason recorded there. A company running one ladder under two names
// would still hold two records here, and the codes are independently unique — `G7` as a job grade and `G7`
// as a salary grade are different rows in different tables, and neither collides with the other.
public sealed class SalaryGradeCode : ValueObject
{
  public const int MaximumLength = 32;

  private SalaryGradeCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<SalaryGradeCode> Create(string? value) =>
    OrganizationalText.TryNormalizeCode(value, MaximumLength, out var trimmed, out var normalized)
      ? Result.Success(new SalaryGradeCode(trimmed, normalized))
      : Result.Failure<SalaryGradeCode>(PositionErrors.InvalidSalaryGradeCode);

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}
