using SSAS.BuildingBlocks.Application.Pagination;

namespace SSAS.Platform.Application.Localization;

public sealed record LocalizationAdministrationResource(
  string ResourceKey,
  string Module,
  string Group,
  string Category,
  string TextFormat,
  string Lifecycle,
  string SecurityClassification,
  bool TenantOverridable,
  int ResourceVersion,
  long CatalogVersion,
  string RequestedCulture,
  string RequestedCultureSystemDefault,
  string EffectiveValue,
  string? CurrentTenantOverride,
  bool? OverrideActive,
  bool Compatibility,
  long? TenantOverrideVersion,
  long? CurrentVersionNumber,
  byte[]? RowVersion,
  DateTimeOffset? LastModifiedUtc,
  IReadOnlyList<string> Placeholders,
  long? EligibleUndoTargetVersion);

public sealed record LocalizationAdministrationDetail(
  LocalizationAdministrationResource Resource,
  string EnglishSystemDefault,
  string ArabicSystemDefault);

public sealed record LocalizationAdministrationHistoryPage(
  LocalizationHistoryResult History,
  PagedResult<LocalizationHistoryEntry> Page);
