using System.Collections.Frozen;

namespace SSAS.BuildingBlocks.Localization.Catalog;

public sealed class LocalizationCatalog : ILocalizationCatalog
{
  private readonly FrozenDictionary<string, LocalizationResourceDefinition> resourcesByKey;
  private readonly FrozenDictionary<string, string> neutralFallbacks;

  public LocalizationCatalog(
    CatalogSchemaVersion catalogSchemaVersion,
    CatalogVersion catalogVersion,
    IEnumerable<LocalizationResourceDefinition> resources,
    string englishNeutralFallback,
    string arabicNeutralFallback)
  {
    CatalogSchemaVersion = catalogSchemaVersion;
    CatalogVersion = catalogVersion;
    Resources = resources.OrderBy(resource => resource.ResourceKey.Value, StringComparer.Ordinal).ToArray();
    resourcesByKey = Resources.ToFrozenDictionary(resource => resource.ResourceKey.Value, StringComparer.Ordinal);
    neutralFallbacks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      [LocalizationCulture.EnglishCode] = englishNeutralFallback,
      [LocalizationCulture.ArabicCode] = arabicNeutralFallback
    }.ToFrozenDictionary(StringComparer.Ordinal);
  }

  public CatalogSchemaVersion CatalogSchemaVersion { get; }

  public CatalogVersion CatalogVersion { get; }

  public IReadOnlyList<LocalizationResourceDefinition> Resources { get; }

  public string GetNeutralFallback(LocalizationCulture culture) => neutralFallbacks[culture.Value];

  public bool TryGet(ResourceKey resourceKey, out LocalizationResourceDefinition resource) =>
    resourcesByKey.TryGetValue(resourceKey.Value, out resource!);

  public IReadOnlyList<LocalizationResourceDefinition> GetActiveGroup(string moduleName, string group) =>
    Resources.Where(resource =>
        resource.Lifecycle == LocalizationResourceLifecycle.Active &&
        string.Equals(resource.Module, moduleName, StringComparison.Ordinal) &&
        string.Equals(resource.Group, group, StringComparison.Ordinal))
      .ToArray();
}
