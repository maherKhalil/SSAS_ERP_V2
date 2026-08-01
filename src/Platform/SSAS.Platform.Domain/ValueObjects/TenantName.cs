using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class TenantName : ValueObject
{
  public const int MaximumLength = 200;

  private TenantName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<TenantName> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumLength
      ? Result.Failure<TenantName>(TenantLifecycleErrors.InvalidTenantName)
      : Result.Success(new TenantName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
