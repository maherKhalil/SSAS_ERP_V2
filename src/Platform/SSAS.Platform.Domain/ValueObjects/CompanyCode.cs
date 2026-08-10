using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class CompanyCode : ValueObject
{
  public const int MaximumLength = 64;

  private CompanyCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<CompanyCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<CompanyCode>(CompanyErrors.InvalidCompanyCode);
    }

    // Normalization is exactly Trim().ToUpperInvariant(); no Unicode NFC/NFD/NFKC/NFKD normalization is applied.
    var normalized = trimmed.ToUpperInvariant();
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<CompanyCode>(CompanyErrors.InvalidCompanyCode);
    }

    return Result.Success(new CompanyCode(trimmed, normalized));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }

  private static bool ContainsControlCharacter(string value)
  {
    foreach (var character in value)
    {
      if (char.IsControl(character))
      {
        return true;
      }
    }

    return false;
  }
}
