using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Branches;

namespace SSAS.Platform.Domain.ValueObjects;

// The human label. Deliberately NOT unique: two branches may legitimately share a display name while their
// codes differ, and forcing uniqueness here would reject real estates rather than protect anything.
public sealed class BranchName : ValueObject
{
  public const int MaximumLength = 256;

  private BranchName(string value) => Value = value;

  public string Value { get; }

  public static Result<BranchName> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed)
      ? Result.Failure<BranchName>(BranchErrors.InvalidBranchName)
      : Result.Success(new BranchName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
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
