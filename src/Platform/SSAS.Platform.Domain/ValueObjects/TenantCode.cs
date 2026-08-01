using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class TenantCode : ValueObject
{
  public const int MaximumLength = 64;

  private TenantCode(string value)
  {
    Value = value;
    NormalizedValue = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<TenantCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumLength
      ? Result.Failure<TenantCode>(TenantLifecycleErrors.InvalidTenantCode)
      : Result.Success(new TenantCode(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}
