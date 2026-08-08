using SSAS.BuildingBlocks.Localization.Catalog;

namespace SSAS.Localization.CatalogTool;

internal sealed record CatalogValidationResult(
  LocalizationCatalogDocument? Document,
  IReadOnlyList<LocalizationResourceDefinition> Resources,
  IReadOnlyList<string> Errors)
{
  public bool IsValid => Document is not null && Errors.Count == 0;
}
