using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class UserDisplayName : ValueObject
{
  private UserDisplayName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<UserDisplayName> Create(string value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 200
      ? Result.Failure<UserDisplayName>(IdentityAccessErrors.InvalidDisplayName)
      : Result.Success(new UserDisplayName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
