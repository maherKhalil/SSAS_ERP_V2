using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed partial class ResourceKey : ValueObject
{
  public const int MaximumLength = 200;

  private ResourceKey(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<ResourceKey> Create(string? value)
  {
    return value is { Length: > 0 and <= MaximumLength } && ResourceKeyPattern().IsMatch(value)
      ? Result.Success(new ResourceKey(value))
      : Result.Failure<ResourceKey>(LocalizationErrors.InvalidResourceKey);
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }

  [GeneratedRegex("^[a-z][a-z0-9_]*(?:\\.[a-z][a-z0-9_]*){2,}$", RegexOptions.CultureInvariant)]
  private static partial Regex ResourceKeyPattern();
}
