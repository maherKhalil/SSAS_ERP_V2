using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Branches;

namespace SSAS.Platform.Domain.ValueObjects;

// Follows CompanyCode exactly: the same normalization rule, the same length discipline, the same control
// character refusal. Uniqueness is enforced per tenant on the NORMALIZED value, so "riyadh" and "RIYADH"
// cannot both exist — a user choosing a branch from a list must be able to tell them apart.
public sealed class BranchCode : ValueObject
{
  public const int MaximumLength = 64;

  private BranchCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<BranchCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<BranchCode>(BranchErrors.InvalidBranchCode);
    }

    // Normalization is exactly Trim().ToUpperInvariant(); no Unicode normalization is applied.
    var normalized = trimmed.ToUpperInvariant();
    return normalized.Length > MaximumLength
      ? Result.Failure<BranchCode>(BranchErrors.InvalidBranchCode)
      : Result.Success(new BranchCode(trimmed, normalized));
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
