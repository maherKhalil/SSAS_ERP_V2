using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed class LocalizationCulture : ValueObject
{
  public const string EnglishCode = "en";
  public const string ArabicCode = "ar";

  private LocalizationCulture(string value)
  {
    Value = value;
  }

  public static LocalizationCulture English { get; } = new(EnglishCode);

  public static LocalizationCulture Arabic { get; } = new(ArabicCode);

  public string Value { get; }

  public TextDirection Direction => Value == ArabicCode ? TextDirection.Rtl : TextDirection.Ltr;

  public static Result<LocalizationCulture> Create(string? value) => value switch
  {
    EnglishCode => Result.Success(English),
    ArabicCode => Result.Success(Arabic),
    _ => Result.Failure<LocalizationCulture>(LocalizationErrors.UnsupportedCulture)
  };

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
