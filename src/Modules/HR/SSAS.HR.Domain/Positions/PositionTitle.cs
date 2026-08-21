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

  private PositionTitle(string value)
  {
    Value = value;
  }

  // Trimmed, with display casing preserved exactly as entered.
  public string Value { get; }

  public static Result<PositionTitle> Create(string? value)
  {
    var trimmed = OrganizationalText.NormalizeLabel(value, MaximumLength);
    return trimmed is null
      ? Result.Failure<PositionTitle>(PositionErrors.InvalidTitle)
      : Result.Success(new PositionTitle(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
