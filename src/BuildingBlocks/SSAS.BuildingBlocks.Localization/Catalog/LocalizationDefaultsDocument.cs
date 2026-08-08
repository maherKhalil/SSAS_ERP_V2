using System.Text.Json.Serialization;

namespace SSAS.BuildingBlocks.Localization.Catalog;

public sealed record LocalizationDefaultsDocument(
  [property: JsonPropertyName("en")] string English,
  [property: JsonPropertyName("ar")] string Arabic);
