namespace SSAS.Localization.CatalogTool;

public static class Program
{
  public static async Task<int> Main(string[] args) => await CatalogToolRunner.RunAsync(args);
}
