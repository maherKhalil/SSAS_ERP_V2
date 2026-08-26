using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class PlanName : ValueObject
{
  public const int MaximumLength = 200;

  private PlanName(string value) => Value = value;

  public string Value { get; }

  public static Result<PlanName> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumLength
      ? Result.Failure<PlanName>(SubscriptionErrors.InvalidPlanName)
      : Result.Success(new PlanName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
