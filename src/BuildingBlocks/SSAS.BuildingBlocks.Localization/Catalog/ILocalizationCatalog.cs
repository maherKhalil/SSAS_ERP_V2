namespace SSAS.BuildingBlocks.Localization.Catalog;

public interface ILocalizationCatalog
{
  CatalogSchemaVersion CatalogSchemaVersion { get; }

  CatalogVersion CatalogVersion { get; }

  IReadOnlyList<LocalizationResourceDefinition> Resources { get; }

  string GetNeutralFallback(LocalizationCulture culture);

  bool TryGet(ResourceKey resourceKey, out LocalizationResourceDefinition resource);

  IReadOnlyList<LocalizationResourceDefinition> GetActiveGroup(string moduleName, string group);
}
