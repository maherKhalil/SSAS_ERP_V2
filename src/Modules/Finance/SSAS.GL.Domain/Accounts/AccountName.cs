using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Accounts;

// THE ACCOUNT'S DISPLAY NAME (REQ-GL-0005, REQ-GL-0006).
//
// Unlike `AccountCode` this carries NO normalized shadow, because nothing is unique by name and nothing
// looks an account up by it. A normalized column exists to back an ordinal index; adding one here would be
// storage in support of a constraint that does not exist.
//
// `nvarchar` throughout (`DEC-GL-0006`): `Constraints.md` requires Arabic and English, and an account name is
// exactly the field a user writes in their own language.
public sealed class AccountName : ValueObject
{
  public const int MaximumLength = 256;

  private AccountName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<AccountName> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<AccountName>(AccountErrors.InvalidName);
    }

    return Result.Success(new AccountName(trimmed));
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
