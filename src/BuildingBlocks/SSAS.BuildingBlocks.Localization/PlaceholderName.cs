using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed partial class PlaceholderName : ValueObject
{
  public const int MaximumLength = 64;

  private PlaceholderName(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<PlaceholderName> Create(string? value) =>
    value is not null && PlaceholderPattern().IsMatch(value)
      ? Result.Success(new PlaceholderName(value))
      : Result.Failure<PlaceholderName>(LocalizationErrors.InvalidPlaceholder);

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }

  [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
  private static partial Regex PlaceholderPattern();
}
