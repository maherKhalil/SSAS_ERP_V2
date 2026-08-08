namespace SSAS.BuildingBlocks.Localization;

public sealed record EffectiveLocalizedText(
  ResourceKey ResourceKey,
  LocalizationCulture RequestedCulture,
  LocalizationCulture ResolvedCulture,
  string Text,
  LocalizationResolutionSource ResolutionSource,
  CatalogVersion CatalogVersion,
  ResourceVersion ResourceVersion,
  TenantLocalizationVersion? TenantLocalizationVersion,
  TenantOverrideVersion? TenantOverrideVersion,
  TextDirection TextDirection,
  bool UsedFallback,
  bool OverrideCompatible);
