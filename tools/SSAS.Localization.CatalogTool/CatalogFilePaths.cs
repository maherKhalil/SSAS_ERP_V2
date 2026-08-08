namespace SSAS.Localization.CatalogTool;

internal sealed record CatalogFilePaths(string Manifest, string Schema, string Backend, string Client)
{
  public static CatalogFilePaths FromArguments(IReadOnlyList<string> arguments)
  {
    var root = Directory.GetCurrentDirectory();
    var catalog = Path.Combine(root, "src", "BuildingBlocks", "SSAS.BuildingBlocks.Localization", "Catalog");
    var generated = Path.Combine(root, "src", "BuildingBlocks", "SSAS.BuildingBlocks.Localization", "Generated");
    return new CatalogFilePaths(
      Get(arguments, "--manifest") ?? Path.Combine(catalog, "localization-catalog.json"),
      Get(arguments, "--schema") ?? Path.Combine(catalog, "localization-catalog.schema.v1.json"),
      Get(arguments, "--backend") ?? Path.Combine(generated, "LocalizationCatalog.Generated.cs"),
      Get(arguments, "--client") ?? Path.Combine(generated, "localization-catalog.client.generated.json"));
  }

  public static string? Get(IReadOnlyList<string> arguments, string name)
  {
    for (var index = 0; index < arguments.Count - 1; index++)
    {
      if (string.Equals(arguments[index], name, StringComparison.Ordinal))
      {
        return arguments[index + 1];
      }
    }

    return null;
  }
}
