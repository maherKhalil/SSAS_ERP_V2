using System.Text.Json.Serialization;

namespace SSAS.BuildingBlocks.Localization.Catalog;

public sealed record LocalizationCatalogDocument(
  [property: JsonPropertyName("$schema")] string Schema,
  [property: JsonPropertyName("catalogSchemaVersion")] int CatalogSchemaVersion,
  [property: JsonPropertyName("catalogVersion")] long CatalogVersion,
  [property: JsonPropertyName("neutralFallbacks")] LocalizationNeutralFallbackDocument NeutralFallbacks,
  [property: JsonPropertyName("resources")] IReadOnlyList<LocalizationResourceDocument> Resources);
