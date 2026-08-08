using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed class LocalizationText : ValueObject
{
  public const int PlainTextMaximumLength = 512;
  public const int MultilineTextMaximumLength = 4000;

  private LocalizationText(string value, LocalizationTextFormat format, PlaceholderSet placeholders)
  {
    Value = value;
    Format = format;
    Placeholders = placeholders;
  }

  public string Value { get; }

  public LocalizationTextFormat Format { get; }

  public PlaceholderSet Placeholders { get; }

  public static Result<LocalizationText> Create(string? value, LocalizationTextFormat format)
  {
    if (value is null || !Enum.IsDefined(format))
    {
      return Result.Failure<LocalizationText>(LocalizationErrors.InvalidText);
    }

    var maximumLength = format == LocalizationTextFormat.PlainText
      ? PlainTextMaximumLength
      : MultilineTextMaximumLength;
    if (value.Length > maximumLength || !HasValidCharacters(value, format))
    {
      return Result.Failure<LocalizationText>(LocalizationErrors.InvalidText);
    }

    var placeholders = LocalizationPlaceholderParser.Parse(value);
    return placeholders.IsSuccess
      ? Result.Success(new LocalizationText(value, format, placeholders.Value))
      : Result.Failure<LocalizationText>(placeholders.Error);
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
    yield return Format;
  }

  private static bool HasValidCharacters(string value, LocalizationTextFormat format)
  {
    for (var index = 0; index < value.Length; index++)
    {
      var character = value[index];
      if (char.IsHighSurrogate(character))
      {
        if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
        {
          return false;
        }

        index++;
        continue;
      }

      if (char.IsLowSurrogate(character))
      {
        return false;
      }

      if (char.IsControl(character) &&
        !(format == LocalizationTextFormat.MultilineText && character is '\r' or '\n' or '\t'))
      {
        return false;
      }
    }

    return true;
  }
}
