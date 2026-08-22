using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE JOB GRADE'S BUSINESS IDENTIFIER (REQ-HR-0201, DEC-POS-0007).
//
// A DISTINCT TYPE FROM `PositionCode` AND `SalaryGradeCode`, deliberately: the three ladders are separate
// aggregates under `DEC-POS-0005`, and a code accepted for one must not be assignable to another. The
// validation is shared through `OrganizationalText` so the three cannot drift apart on what "collides"
// means; the TYPES are not shared, so the compiler keeps them apart.
//
// Unique within a company, never generated, ordinal-normalized. Same rules as `PositionCode`.
public sealed class JobGradeCode : ValueObject
{
  public const int MaximumLength = 32;

  private JobGradeCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<JobGradeCode> Create(string? value) =>
    OrganizationalText.TryNormalizeCode(value, MaximumLength, out var trimmed, out var normalized)
      ? Result.Success(new JobGradeCode(trimmed, normalized))
      : Result.Failure<JobGradeCode>(PositionErrors.InvalidJobGradeCode);

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}
