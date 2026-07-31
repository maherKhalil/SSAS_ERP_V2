using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class PermissionName : ValueObject
{
  private PermissionName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<PermissionName> Create(string value)
  {
    return IsValid(value)
      ? Result.Success(new PermissionName(value))
      : Result.Failure<PermissionName>(IdentityAccessErrors.InvalidPermission);
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }

  private static bool IsValid(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
    {
      return false;
    }

    var segments = value.Split('.', StringSplitOptions.None);
    return segments.Length == 3 && segments.All(IsIdentifierSegment);
  }

  private static bool IsIdentifierSegment(string segment)
  {
    return segment.Length > 0 && char.IsAsciiLetter(segment[0]) &&
      segment.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
  }
}
