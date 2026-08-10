using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class CompanyName : ValueObject
{
  public const int MaximumLength = 200;

  private CompanyName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<CompanyName> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumLength
      ? Result.Failure<CompanyName>(CompanyErrors.InvalidCompanyName)
      : Result.Success(new CompanyName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
