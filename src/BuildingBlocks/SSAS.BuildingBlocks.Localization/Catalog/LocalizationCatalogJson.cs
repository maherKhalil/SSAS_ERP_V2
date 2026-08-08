using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSAS.BuildingBlocks.Localization.Catalog;

public static class LocalizationCatalogJson
{
  public static JsonSerializerOptions CreateOptions() => new()
  {
    AllowTrailingCommas = false,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNameCaseInsensitive = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
  };
}
