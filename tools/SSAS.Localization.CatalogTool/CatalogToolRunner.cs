namespace SSAS.Localization.CatalogTool;

internal static class CatalogToolRunner
{
  public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
  {
    if (args.Length == 0)
    {
      WriteUsage();
      return 1;
    }

    var command = args[0];
    var paths = CatalogFilePaths.FromArguments(args);
    var validation = await SemanticCatalogValidator.ValidateAsync(paths.Manifest, paths.Schema, cancellationToken);
    if (!validation.IsValid)
    {
      foreach (var error in validation.Errors)
      {
        Console.Error.WriteLine(error);
      }

      return 1;
    }

    if (string.Equals(command, "validate", StringComparison.Ordinal))
    {
      Console.WriteLine($"Catalog valid: {validation.Resources.Count} resources, version {validation.Document!.CatalogVersion}.");
      return 0;
    }

    if (string.Equals(command, "generate", StringComparison.Ordinal))
    {
      var generated = GeneratedCatalogWriter.Generate(validation);
      Directory.CreateDirectory(Path.GetDirectoryName(paths.Backend)!);
      await File.WriteAllBytesAsync(paths.Backend, generated.Backend, cancellationToken);
      await File.WriteAllBytesAsync(paths.Client, generated.Client, cancellationToken);
      Console.WriteLine("Generated backend and neutral-client localization artifacts.");
      return 0;
    }

    if (string.Equals(command, "verify", StringComparison.Ordinal))
    {
      var generated = GeneratedCatalogWriter.Generate(validation);
      var backendMatches = File.Exists(paths.Backend) &&
        (await File.ReadAllBytesAsync(paths.Backend, cancellationToken)).AsSpan().SequenceEqual(generated.Backend);
      var clientMatches = File.Exists(paths.Client) &&
        (await File.ReadAllBytesAsync(paths.Client, cancellationToken)).AsSpan().SequenceEqual(generated.Client);
      if (!backendMatches || !clientMatches)
      {
        Console.Error.WriteLine("Generated localization artifacts are stale.");
        return 1;
      }

      Console.WriteLine("Generated localization artifacts are current.");
      return 0;
    }

    if (string.Equals(command, "impact", StringComparison.Ordinal))
    {
      var baselinePath = CatalogFilePaths.Get(args, "--baseline");
      if (string.IsNullOrWhiteSpace(baselinePath))
      {
        Console.Error.WriteLine("impact requires --baseline <manifest>.");
        return 1;
      }

      var baseline = await SemanticCatalogValidator.ValidateAsync(baselinePath, paths.Schema, cancellationToken);
      if (!baseline.IsValid)
      {
        foreach (var error in baseline.Errors)
        {
          Console.Error.WriteLine($"Baseline: {error}");
        }

        return 1;
      }

      var impacts = CatalogImpactAnalyzer.Analyze(baseline, validation);
      foreach (var impact in impacts)
      {
        Console.WriteLine($"{impact.Kind}: {impact.ResourceKey}");
      }

      return impacts.Any(impact => impact.Kind is CatalogImpactKind.SecuritySensitiveIncompatible or CatalogImpactKind.RemovedProhibited)
        ? 2
        : 0;
    }

    WriteUsage();
    return 1;
  }

  private static void WriteUsage() =>
    Console.Error.WriteLine("Usage: validate|generate|verify|impact [--manifest path] [--schema path] [--backend path] [--client path] [--baseline path]");
}
