using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE POSITION'S DISPLAY TITLE (REQ-HR-0200, BRULE-POS-0005).
//
// NOT UNIQUE, and not normalized for comparison. Two positions in one company may legitimately share a
// title — an "Accountant" in one part of the business and an "Accountant" in another are different jobs at
// possibly different grades — and the CODE is what distinguishes them. Forcing uniqueness would make
// "Accountant" available to exactly one part of the company, which is a shape organizations actually have.
public sealed class PositionTitle : ValueObject
{
  public const int MaximumLength = 128;

  private PositionTitle(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // Trimmed, with display casing preserved exactly as entered.
  public string Value { get; }

  // Upper-invariant and trimmed, for SEARCH and nothing else (`DEC-POS-0030`). It backs no index and no
  // uniqueness rule: two records may share a label forever. It exists because the stored column is
  // binary-collated, so a case-insensitive match needs a normalized column rather than a normalized query.
  public string NormalizedValue { get; }

  public static Result<PositionTitle> Create(string? value)
  {
    return OrganizationalText.TryNormalizeLabel(value, MaximumLength, out var trimmed, out var normalized)
      ? Result.Success(new PositionTitle(trimmed, normalized))
      : Result.Failure<PositionTitle>(PositionErrors.InvalidTitle);
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
