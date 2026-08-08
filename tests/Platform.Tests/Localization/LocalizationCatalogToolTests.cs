using SSAS.Localization.CatalogTool;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Generated;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationCatalogToolTests
{
  [Fact]
  public async Task Validate_and_verify_accept_checked_in_artifacts()
  {
    var paths = GetPaths();

    Assert.Equal(0, await CatalogToolRunner.RunAsync(["validate", "--manifest", paths.Manifest, "--schema", paths.Schema]));
    Assert.Equal(0, await CatalogToolRunner.RunAsync([
      "verify", "--manifest", paths.Manifest, "--schema", paths.Schema, "--backend", paths.Backend, "--client", paths.Client]));
  }

  [Fact]
  public async Task Verify_rejects_stale_artifact_and_generate_is_deterministic()
  {
    var source = GetPaths();
    var temporary = Path.Combine(Path.GetTempPath(), $"ssas-localization-{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporary);
    try
    {
      var manifest = Path.Combine(temporary, "catalog.json");
      var schema = Path.Combine(temporary, "schema.json");
      var backend = Path.Combine(temporary, "generated.cs");
      var client = Path.Combine(temporary, "generated.json");
      File.Copy(source.Manifest, manifest);
      File.Copy(source.Schema, schema);
      File.Copy(source.Backend, backend);
      File.Copy(source.Client, client);
      await File.AppendAllTextAsync(backend, "stale");

      Assert.Equal(1, await CatalogToolRunner.RunAsync([
        "verify", "--manifest", manifest, "--schema", schema, "--backend", backend, "--client", client]));
      Assert.Equal(0, await CatalogToolRunner.RunAsync([
        "generate", "--manifest", manifest, "--schema", schema, "--backend", backend, "--client", client]));
      var firstBackend = await File.ReadAllBytesAsync(backend);
      var firstClient = await File.ReadAllBytesAsync(client);
      Assert.Equal(0, await CatalogToolRunner.RunAsync([
        "generate", "--manifest", manifest, "--schema", schema, "--backend", backend, "--client", client]));
      Assert.Equal(firstBackend, await File.ReadAllBytesAsync(backend));
      Assert.Equal(firstClient, await File.ReadAllBytesAsync(client));
    }
    finally
    {
      Directory.Delete(temporary, true);
    }
  }

  [Fact]
  public void Impact_analysis_classifies_release_changes_and_security_blockers()
  {
    var resources = GeneratedLocalizationCatalog.Instance.Resources;
    var authentication = resources.Single(resource => resource.ResourceKey.Value.EndsWith("authentication_failed", StringComparison.Ordinal));
    var requestRejected = resources.Single(resource => resource.ResourceKey.Value.EndsWith("request_rejected", StringComparison.Ordinal));
    var cancel = resources.Single(resource => resource.ResourceKey.Value.EndsWith("cancel", StringComparison.Ordinal));
    var save = resources.Single(resource => resource.ResourceKey.Value.EndsWith("save", StringComparison.Ordinal));
    var required = resources.Single(resource => resource.ResourceKey.Value.EndsWith("required", StringComparison.Ordinal));
    var incompatibleFingerprint = CompatibilityFingerprint.FromBytes(new byte[32]).Value;
    var added = cancel with
    {
      ResourceKey = ResourceKey.Create("platform.common.actions.continue").Value
    };
    var baseline = new CatalogValidationResult(null, [authentication, requestRejected, cancel, save, required], []);
    var candidate = new CatalogValidationResult(null,
    [
      authentication with { CompatibilityFingerprint = incompatibleFingerprint },
      cancel with { EnglishDefault = "Cancel now", ResourceVersion = ResourceVersion.Create(2).Value },
      save with { CompatibilityFingerprint = incompatibleFingerprint },
      required with { Lifecycle = LocalizationResourceLifecycle.Retired },
      added
    ], []);

    var impacts = CatalogImpactAnalyzer.Analyze(baseline, candidate);

    Assert.Equal(
      [
        CatalogImpactKind.SecuritySensitiveIncompatible,
        CatalogImpactKind.RemovedProhibited,
        CatalogImpactKind.ChangedCompatible,
        CatalogImpactKind.Added,
        CatalogImpactKind.ChangedIncompatible,
        CatalogImpactKind.Retired
      ],
      impacts.Select(impact => impact.Kind));
    Assert.Contains(impacts, impact => impact.Kind == CatalogImpactKind.SecuritySensitiveIncompatible);
  }

  private static (string Manifest, string Schema, string Backend, string Client) GetPaths()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    var root = Assert.IsType<DirectoryInfo>(directory).FullName;
    var project = Path.Combine(root, "src", "BuildingBlocks", "SSAS.BuildingBlocks.Localization");
    return (
      Path.Combine(project, "Catalog", "localization-catalog.json"),
      Path.Combine(project, "Catalog", "localization-catalog.schema.v1.json"),
      Path.Combine(project, "Generated", "LocalizationCatalog.Generated.cs"),
      Path.Combine(project, "Generated", "localization-catalog.client.generated.json"));
  }
}
