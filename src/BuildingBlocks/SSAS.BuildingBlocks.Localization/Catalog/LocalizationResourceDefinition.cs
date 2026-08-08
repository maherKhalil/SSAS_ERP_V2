namespace SSAS.BuildingBlocks.Localization.Catalog;

public sealed record LocalizationResourceDefinition(
  ResourceKey ResourceKey,
  string Module,
  string Group,
  LocalizationResourceCategory Category,
  LocalizationTextFormat TextFormat,
  LocalizationSecurityClassification SecurityClassification,
  bool TenantOverridable,
  LocalizationResourceLifecycle Lifecycle,
  ResourceVersion ResourceVersion,
  string EnglishDefault,
  string ArabicDefault,
  PlaceholderSet Placeholders,
  PlaceholderFingerprint PlaceholderFingerprint,
  CompatibilityFingerprint CompatibilityFingerprint,
  string? Description)
{
  public string GetDefault(LocalizationCulture culture) =>
    culture.Value == LocalizationCulture.ArabicCode ? ArabicDefault : EnglishDefault;

  public bool IsTenantEditable =>
    Lifecycle == LocalizationResourceLifecycle.Active &&
    SecurityClassification == LocalizationSecurityClassification.Ordinary &&
    TenantOverridable;
}
