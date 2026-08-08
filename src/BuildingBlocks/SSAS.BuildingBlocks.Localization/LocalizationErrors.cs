using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public static class LocalizationErrors
{
  public static readonly Error InvalidResourceKey = new("localization.resource_key_invalid", "The localization resource key is invalid.");
  public static readonly Error UnsupportedCulture = new("localization.culture_unsupported", "The localization culture is unsupported.");
  public static readonly Error InvalidPlaceholder = new("localization.placeholder_invalid", "The localization placeholder syntax is invalid.");
  public static readonly Error PlaceholderMismatch = new("localization.placeholder_mismatch", "The supplied placeholder names do not match the resource contract.");
  public static readonly Error InvalidText = new("localization.text_invalid", "The localized text is invalid for its format.");
  public static readonly Error VersionInvalid = new("localization.version_invalid", "The localization version must be positive.");
  public static readonly Error VersionOverflow = new("localization.version_overflow", "The localization version cannot be incremented.");
}
