using SSAS.Localization.CatalogTool;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Generated;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationCatalogToolTests
{
  // ⚠ THIS IS THE ACCEPT HALF AND IT MEANS NOTHING WITHOUT THE REJECT HALF BELOW. `validate` and `verify`
  // both return 0 for the checked-in manifest, schema and generated artifacts — which is exactly what a
  // tool that always returned 0 would also do. `Verify_rejects_stale_artifact_and_generate_is_deterministic`
  // appends a byte to the backend artifact and requires exit code **1**, and that is what makes this test a
  // statement about the artifacts rather than about the tool's willingness to succeed.
  //
  // ⚠⚠ Cited to nothing on its own: *the checked-in artifacts are current* is a repository fact, not an
  // acceptance criterion. Its value is as the anti-vacuity partner of the determinism citation below —
  // recorded here because the dependency runs in the direction a reader would not guess, the ACCEPT test
  // depending on the REJECT test for its meaning.
  [Fact]
  public async Task Validate_and_verify_accept_checked_in_artifacts()
  {
    var paths = GetPaths();

    Assert.Equal(0, await CatalogToolRunner.RunAsync(["validate", "--manifest", paths.Manifest, "--schema", paths.Schema]));
    Assert.Equal(0, await CatalogToolRunner.RunAsync([
      "verify", "--manifest", paths.Manifest, "--schema", paths.Schema, "--backend", paths.Backend, "--client", paths.Client]));
  }

  // ⚠ CITES THE SECOND CLAUSE OF `AC-LOC-0002` — *"…and GENERATES BYTE-DETERMINISTIC ARTIFACTS."* Two
  // consecutive `generate` runs over the same manifest, compared with `ReadAllBytesAsync`.
  //
  // ⚠⚠ **`Assert.Equal` OVER `byte[]` IS THE CITATION, NOT A DETAIL OF IT.** The criterion says
  // BYTE-deterministic, and a comparison of the files' TEXT would pass over a change of line endings while
  // the artifacts differed byte for byte — which is precisely the difference the word exists to exclude.
  // A string comparison here would be the same assertion in shape and a weaker one in fact.
  //
  // ⚠⚠⚠ AND THE FIRST CLAUSE IS **NOT** CARRIED HERE. `AC-LOC-0002` reads *"Manifest validation REJECTS
  // duplicate/reordered, invalid, renamed, or reused retired ResourceKeys AND generates byte-deterministic
  // artifacts"* — a five-item rejection list joined to a generation property. **The stale-artifact leg below
  // is not one of those five**: it rejects a DRIFTED GENERATED FILE, not a malformed manifest. The five are
  // `LocalizationCatalogTests`' subject. **Cited as 1 of 2 clauses, with the untested clause being itself a
  // five-item list** — the collective-predicate shape again, in the half this file does not carry.
  //
  // The stale leg's own value is as the anti-vacuity partner named above: `verify` returning **1** for a
  // mutated artifact is what makes `verify` returning 0 in the previous test a statement about artifacts.
  [Fact]
  [Trait("Criterion", "AC-LOC-0002")]
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

  // ⚠ EXAMINED AND UNCITED, AND THE REASON IS THE INTERESTING ONE: **NO ACCEPTANCE CRITERION MENTIONS
  // IMPACT ANALYSIS AT ALL.** `CatalogImpactAnalyzer` classifies a release into `SecuritySensitive
  // Incompatible`, `RemovedProhibited`, `ChangedIncompatible`, `ChangedCompatible`, `Added` and `Retired`,
  // and that taxonomy appears nowhere in `acceptance-criteria.md`. Adjacent criteria exist — `AC-LOC-0002`
  // on the key boundary, `AC-LOC-0031` on the compatibility fingerprint, `AC-LOC-0041` on retirement — but
  // each governs a RULE this analyzer REPORTS ON, not the reporting. **Citing one would be adjacent-scope.**
  //
  // ⚠⚠ Same shape as `RequestTenantEligibilityTests`' locked-read guard: a real mechanism with real tests
  // and nothing to cite. **A citation census is not a coverage census, and this direction of the gap —
  // covered, uncitable — cannot be closed by citing harder.**
  //
  // ⚠⚠⚠ AND ONE ASSERTION HERE CANNOT FAIL INDEPENDENTLY. The `Assert.Contains(… SecuritySensitive
  // Incompatible)` at the end is SUBSUMED by the ordered `Assert.Equal` above it, which already pins that
  // kind in first position — if the sequence equality passes, the `Contains` cannot fail, and if it fails
  // the `Contains` is never reached. **It is emphasis, not a check.** Left in place because it names the
  // security case for a reader, but it should not be read as a second guarantee: the ordered comparison is
  // the whole of the test's power, and it is a STRONGER claim than membership because it pins ORDER.
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
