using System.Text.Json.Serialization;

namespace SSAS.Platform.API.Localization;

public sealed record PutLocalizationOverrideRequest(
  [property: JsonPropertyName("value")] string? Value,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record UndoLocalizationOverrideRequest(
  [property: JsonPropertyName("targetVersionNumber")] long? TargetVersionNumber,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record RestoreLocalizationOverrideDefaultRequest(
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record PreviewLocalizationRequest(
  [property: JsonPropertyName("resourceKey")] string? ResourceKey,
  [property: JsonPropertyName("culture")] string? Culture,
  [property: JsonPropertyName("value")] string? Value);

public sealed record EffectiveLocalizationBatchRequest(
  [property: JsonPropertyName("culture")] string? Culture,
  [property: JsonPropertyName("resourceKeys")] IReadOnlyList<string>? ResourceKeys,
  [property: JsonPropertyName("placeholderValuesByResource")]
  IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? PlaceholderValuesByResource);

public sealed record EffectiveLocalizationItemResponse(
  string ResourceKey,
  string Value,
  string RequestedCulture,
  string ResolvedCulture,
  string Direction,
  string Source,
  int ResourceVersion);

public sealed record EffectiveLocalizationResponse(
  string RequestedCulture,
  string? ResolvedCulture,
  string? Direction,
  long CatalogVersion,
  long? TenantLocalizationVersion,
  IReadOnlyCollection<EffectiveLocalizationItemResponse> Items);

public sealed record LocalizationResourceResponse(
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
  string? RowVersion,
  DateTimeOffset? LastModifiedUtc,
  IReadOnlyList<string> Placeholders,
  long? EligibleUndoTargetVersion);

public sealed record LocalizationResourcePageResponse(
  IReadOnlyCollection<LocalizationResourceResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);

public sealed record LocalizationResourceDetailResponse(
  LocalizationResourceResponse Resource,
  string EnglishSystemDefault,
  string ArabicSystemDefault);

public sealed record LocalizationHistoryEntryResponse(
  long VersionNumber,
  string? Value,
  bool IsActive,
  string ChangeType,
  long? PriorLogicalVersionNumber,
  long? UndoTargetVersionNumber,
  long CatalogVersion,
  int ResourceVersion,
  string ChangedBy,
  DateTimeOffset OccurredUtc);

public sealed record LocalizationHistoryResponse(
  string ResourceKey,
  string Culture,
  bool IsActive,
  long CurrentVersionNumber,
  long? EligibleUndoTargetVersion,
  IReadOnlyCollection<LocalizationHistoryEntryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);

public sealed record LocalizationMutationResponse(
  string ResourceKey,
  string Culture,
  string? Value,
  bool IsActive,
  long TenantOverrideVersion,
  long TenantLocalizationVersion,
  string RowVersion);

public sealed record LocalizationPreviewResponse(
  string ResourceKey,
  string Culture,
  string Value,
  string TextFormat,
  IReadOnlyList<string> Placeholders,
  string Direction,
  long CatalogVersion,
  int ResourceVersion,
  string RequestedCulture,
  string ResolvedCulture);
