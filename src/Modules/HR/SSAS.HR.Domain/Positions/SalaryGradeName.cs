using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE SALARY GRADE'S DISPLAY NAME (REQ-HR-0202).
//
// Not unique and not normalized. See `JobGradeName`: the ladder's order is `RankOrder`, not this.
public sealed class SalaryGradeName : ValueObject
{
  public const int MaximumLength = 128;

  private SalaryGradeName(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  // Upper-invariant and trimmed, for SEARCH and nothing else (`DEC-POS-0030`). It backs no index and no
  // uniqueness rule: two records may share a label forever. It exists because the stored column is
  // binary-collated, so a case-insensitive match needs a normalized column rather than a normalized query.
  public string NormalizedValue { get; }

  public static Result<SalaryGradeName> Create(string? value)
  {
    return OrganizationalText.TryNormalizeLabel(value, MaximumLength, out var trimmed, out var normalized)
      ? Result.Success(new SalaryGradeName(trimmed, normalized))
      : Result.Failure<SalaryGradeName>(PositionErrors.InvalidSalaryGradeName);
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
