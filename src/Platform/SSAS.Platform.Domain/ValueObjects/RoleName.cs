using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class RoleName : ValueObject
{
  private RoleName(string value)
  {
    Value = value;
    NormalizedRoleName = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedRoleName { get; }

  public static Result<RoleName> Create(string value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 100
      ? Result.Failure<RoleName>(IdentityAccessErrors.InvalidRoleName)
      : Result.Success(new RoleName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedRoleName;
  }
}
