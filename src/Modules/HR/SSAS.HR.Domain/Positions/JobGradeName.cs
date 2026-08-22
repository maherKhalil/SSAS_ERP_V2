using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE JOB GRADE'S DISPLAY NAME (REQ-HR-0201).
//
// Not unique and not normalized, exactly as `PositionTitle` and `DepartmentName` are. What orders a ladder
// is `RankOrder`, which is authoritative data (`DEC-POS-0006`) — never this name, and never the code.
public sealed class JobGradeName : ValueObject
{
  public const int MaximumLength = 128;

  private JobGradeName(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  // Upper-invariant and trimmed, for SEARCH and nothing else (`DEC-POS-0030`). It backs no index and no
  // uniqueness rule: two records may share a label forever. It exists because the stored column is
  // binary-collated, so a case-insensitive match needs a normalized column rather than a normalized query.
  public string NormalizedValue { get; }

  public static Result<JobGradeName> Create(string? value)
  {
    return OrganizationalText.TryNormalizeLabel(value, MaximumLength, out var trimmed, out var normalized)
      ? Result.Success(new JobGradeName(trimmed, normalized))
      : Result.Failure<JobGradeName>(PositionErrors.InvalidJobGradeName);
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
