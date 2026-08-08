using System.Text.Json.Serialization;

namespace SSAS.BuildingBlocks.Localization.Catalog;

public sealed record LocalizationResourceDocument(
  [property: JsonPropertyName("resourceKey")] string ResourceKey,
  [property: JsonPropertyName("module")] string Module,
  [property: JsonPropertyName("group")] string Group,
  [property: JsonPropertyName("category")] string Category,
  [property: JsonPropertyName("textFormat")] string TextFormat,
  [property: JsonPropertyName("securityClassification")] string SecurityClassification,
  [property: JsonPropertyName("tenantOverridable")] bool TenantOverridable,
  [property: JsonPropertyName("lifecycle")] string Lifecycle,
  [property: JsonPropertyName("resourceVersion")] int ResourceVersion,
  [property: JsonPropertyName("placeholders")] IReadOnlyList<string> Placeholders,
  [property: JsonPropertyName("defaults")] LocalizationDefaultsDocument Defaults,
  [property: JsonPropertyName("description")] string? Description);
