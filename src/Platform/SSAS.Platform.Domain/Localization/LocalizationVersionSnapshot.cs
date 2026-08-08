using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Domain.Localization;

public sealed record LocalizationVersionSnapshot(
  TenantOverrideVersion VersionNumber,
  string? Value,
  bool IsActive,
  LocalizationTextFormat TextFormat,
  TenantOverrideVersion? PriorLogicalVersionNumber,
  TenantOverrideVersion? UndoTargetVersionNumber,
  CatalogVersion CatalogVersion,
  ResourceVersion ResourceVersion,
  PlaceholderFingerprint PlaceholderFingerprint,
  CompatibilityFingerprint CompatibilityFingerprint);
