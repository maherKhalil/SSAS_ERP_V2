using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

// A PLAN'S UNIQUENESS KEY, AND IT CARRIES NO TENANT (FP-014).
//
// `TenantCode`'s shape exactly, with one difference that is the whole point: the uniqueness of a plan code
// is GLOBAL. `ADR-017` § Lookup classification puts plans in class A — Platform global, "tenants cannot
// create global rows" — so the unique index is `(NormalizedPlanCode)` with no `TenantId` beside it.
public sealed class PlanCode : ValueObject
{
  public const int MaximumLength = 64;

  private PlanCode(string value)
  {
    Value = value;
    NormalizedValue = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<PlanCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumLength
      ? Result.Failure<PlanCode>(SubscriptionErrors.InvalidPlanCode)
      : Result.Success(new PlanCode(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}
