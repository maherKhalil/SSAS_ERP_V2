using System.Text.Json;
using System.Security.Cryptography;
using Json.Schema;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;

namespace SSAS.Localization.CatalogTool;

internal static class SemanticCatalogValidator
{
  private static readonly object SchemaLock = new();
  private static readonly Dictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);

  public static async Task<CatalogValidationResult> ValidateAsync(
    string manifestPath,
    string schemaPath,
    CancellationToken cancellationToken = default)
  {
    var errors = new List<string>();
    if (!File.Exists(manifestPath))
    {
      return new(null, [], [$"Manifest does not exist: {manifestPath}"]);
    }

    if (!File.Exists(schemaPath))
    {
      return new(null, [], [$"Schema does not exist: {schemaPath}"]);
    }

    var manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
    if (manifestBytes.Length >= 3 && manifestBytes[0] == 0xEF && manifestBytes[1] == 0xBB && manifestBytes[2] == 0xBF)
    {
      errors.Add("The manifest must be UTF-8 without BOM.");
    }

    JsonDocument manifestJson;
    try
    {
      manifestJson = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions
      {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
      });
    }
    catch (JsonException exception)
    {
      errors.Add($"Malformed JSON: {exception.Message}");
      return new(null, [], errors);
    }

    using (manifestJson)
    {
      FindDuplicateProperties(manifestJson.RootElement, "$", errors);
      try
      {
        var schemaText = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        var schema = GetSchema(schemaText);
        var evaluation = schema.Evaluate(manifestJson.RootElement, new EvaluationOptions
        {
          OutputFormat = OutputFormat.List
        });
        if (!evaluation.IsValid)
        {
          errors.Add("The manifest does not satisfy localization-catalog.schema.v1.json.");
        }
      }
      catch (Exception exception) when (exception is JsonException or InvalidOperationException or JsonSchemaException)
      {
        errors.Add($"Schema validation failed: {exception.Message}");
      }

      LocalizationCatalogDocument? document = null;
      try
      {
        document = manifestJson.Deserialize<LocalizationCatalogDocument>(LocalizationCatalogJson.CreateOptions());
      }
      catch (JsonException exception)
      {
        errors.Add($"Manifest deserialization failed: {exception.Message}");
      }

      if (document is null)
      {
        return new(null, [], errors);
      }

      var resources = ValidateDocument(document, errors);
      return new(document, resources, errors);
    }
  }

  private static JsonSchema GetSchema(string schemaText)
  {
    var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(schemaText)));
    lock (SchemaLock)
    {
      if (!Schemas.TryGetValue(key, out var schema))
      {
        schema = JsonSchema.FromText(schemaText);
        Schemas.Add(key, schema);
      }

      return schema;
    }
  }

  private static List<LocalizationResourceDefinition> ValidateDocument(
    LocalizationCatalogDocument document,
    List<string> errors)
  {
    if (CatalogSchemaVersion.Create(document.CatalogSchemaVersion).IsFailure)
    {
      errors.Add("CatalogSchemaVersion must be positive.");
    }

    if (CatalogVersion.Create(document.CatalogVersion).IsFailure)
    {
      errors.Add("CatalogVersion must be positive.");
    }

    var definitions = new List<LocalizationResourceDefinition>();
    var previous = string.Empty;
    var keys = new HashSet<string>(StringComparer.Ordinal);
    foreach (var resource in document.Resources)
    {
      if (!keys.Add(resource.ResourceKey))
      {
        errors.Add($"Duplicate ResourceKey: {resource.ResourceKey}");
      }

      if (previous.Length > 0 && StringComparer.Ordinal.Compare(previous, resource.ResourceKey) >= 0)
      {
        errors.Add($"Resources are not in strict ordinal order at {resource.ResourceKey}.");
      }

      previous = resource.ResourceKey;
      var key = ResourceKey.Create(resource.ResourceKey);
      var placeholders = PlaceholderSet.Create(resource.Placeholders);
      if (key.IsFailure || placeholders.IsFailure ||
        !Enum.TryParse<LocalizationResourceCategory>(resource.Category, false, out var category) ||
        !Enum.TryParse<LocalizationTextFormat>(resource.TextFormat, false, out var format) ||
        !Enum.TryParse<LocalizationSecurityClassification>(resource.SecurityClassification, false, out var classification) ||
        !Enum.TryParse<LocalizationResourceLifecycle>(resource.Lifecycle, false, out var lifecycle) ||
        ResourceVersion.Create(resource.ResourceVersion).IsFailure)
      {
        errors.Add($"Invalid metadata for {resource.ResourceKey}.");
        continue;
      }

      if (!string.Equals(resource.Module, resource.ResourceKey.Split('.')[0], StringComparison.Ordinal))
      {
        errors.Add($"Module ownership does not match ResourceKey for {resource.ResourceKey}.");
      }

      if (classification == LocalizationSecurityClassification.SecuritySensitiveNonOverridable && resource.TenantOverridable)
      {
        errors.Add($"Security-sensitive resource cannot be Tenant-overridable: {resource.ResourceKey}.");
      }

      if (!resource.Placeholders.SequenceEqual(resource.Placeholders.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
        resource.Placeholders.Count != resource.Placeholders.Distinct(StringComparer.Ordinal).Count())
      {
        errors.Add($"Placeholder metadata must be distinct and ordinally ordered for {resource.ResourceKey}.");
      }

      var english = LocalizationText.Create(resource.Defaults.English, format);
      var arabic = LocalizationText.Create(resource.Defaults.Arabic, format);
      if (english.IsFailure || arabic.IsFailure)
      {
        errors.Add($"Invalid localized default for {resource.ResourceKey}.");
        continue;
      }

      if (!english.Value.Placeholders.Matches(placeholders.Value) || !arabic.Value.Placeholders.Matches(placeholders.Value))
      {
        errors.Add($"Default placeholder sets do not match metadata for {resource.ResourceKey}.");
        continue;
      }

      var placeholderFingerprint = PlaceholderFingerprint.Calculate(placeholders.Value);
      var compatibilityFingerprint = CompatibilityFingerprint.Calculate(
        key.Value,
        format,
        classification,
        resource.TenantOverridable,
        placeholders.Value);
      definitions.Add(new LocalizationResourceDefinition(
        key.Value,
        resource.Module,
        resource.Group,
        category,
        format,
        classification,
        resource.TenantOverridable,
        lifecycle,
        ResourceVersion.Create(resource.ResourceVersion).Value,
        resource.Defaults.English,
        resource.Defaults.Arabic,
        placeholders.Value,
        placeholderFingerprint,
        compatibilityFingerprint,
        resource.Description));
    }

    return definitions;
  }

  private static void FindDuplicateProperties(JsonElement element, string path, List<string> errors)
  {
    if (element.ValueKind == JsonValueKind.Object)
    {
      var names = new HashSet<string>(StringComparer.Ordinal);
      foreach (var property in element.EnumerateObject())
      {
        if (!names.Add(property.Name))
        {
          errors.Add($"Duplicate JSON property at {path}.{property.Name}.");
        }

        FindDuplicateProperties(property.Value, $"{path}.{property.Name}", errors);
      }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
      var index = 0;
      foreach (var item in element.EnumerateArray())
      {
        FindDuplicateProperties(item, $"{path}[{index++}]", errors);
      }
    }
  }
}
