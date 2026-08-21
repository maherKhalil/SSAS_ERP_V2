using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// THE JOB GRADE'S DISPLAY NAME (REQ-HR-0201).
//
// Not unique and not normalized, exactly as `PositionTitle` and `DepartmentName` are. What orders a ladder
// is `RankOrder`, which is authoritative data (`DEC-POS-0006`) — never this name, and never the code.
public sealed class JobGradeName : ValueObject
{
  public const int MaximumLength = 128;

  private JobGradeName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<JobGradeName> Create(string? value)
  {
    var trimmed = OrganizationalText.NormalizeLabel(value, MaximumLength);
    return trimmed is null
      ? Result.Failure<JobGradeName>(PositionErrors.InvalidJobGradeName)
      : Result.Success(new JobGradeName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
