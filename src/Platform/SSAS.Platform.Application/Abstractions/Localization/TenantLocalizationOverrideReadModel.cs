using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Application.Abstractions.Localization;

public sealed record TenantLocalizationOverrideReadModel(
  string ResourceKey,
  string Culture,
  LocalizationTextFormat TextFormat,
  string? Value,
  bool IsActive,
  long TenantOverrideVersion,
  long CatalogVersion,
  int ResourceVersion,
  byte[] PlaceholderFingerprint,
  byte[] CompatibilityFingerprint);
