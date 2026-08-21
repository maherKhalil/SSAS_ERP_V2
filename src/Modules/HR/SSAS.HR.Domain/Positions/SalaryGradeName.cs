using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE SALARY GRADE'S DISPLAY NAME (REQ-HR-0202).
//
// Not unique and not normalized. See `JobGradeName`: the ladder's order is `RankOrder`, not this.
public sealed class SalaryGradeName : ValueObject
{
  public const int MaximumLength = 128;

  private SalaryGradeName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<SalaryGradeName> Create(string? value)
  {
    var trimmed = OrganizationalText.NormalizeLabel(value, MaximumLength);
    return trimmed is null
      ? Result.Failure<SalaryGradeName>(PositionErrors.InvalidSalaryGradeName)
      : Result.Success(new SalaryGradeName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
