using System.Text.Json.Nodes;
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

  // ==================================================================================================
  // ⚠⚠⚠ CLASSIFIED IS NOT BLOCKED, AND THE CRITERION SAYS *BLOCKS*.
  // ==================================================================================================
  //
  // ---- FIRST, A CORRECTION TO THE COMMENT ABOVE `Impact_analysis_classifies_…`, WHICH IS FALSE.
  //
  // It reads *"NO ACCEPTANCE CRITERION MENTIONS IMPACT ANALYSIS AT ALL"* and concludes the analyzer is
  // *covered, uncitable*. **`AC-LOC-0020` is the criterion for exactly this mechanism** — *"Validation
  // reports incompatible retained overrides, BLOCKS SENSITIVE INCOMPATIBILITY, and requires ordinary
  // review."* — and its three clauses map one-to-one onto the analyzer's own vocabulary. The spec says so
  // in three places: `decisions-approved.md` (*"sensitive incompatibility blocks Production"*),
  // `localization-resolution-model.md` (*"Security-sensitive incompatibility blocks Production; ordinary
  // incompatibility requires explicit release review"*) and `requirements.md`.
  //
  // ⚠ **AN ABSENCE CLAIM IS THE ONE THAT ROTS FIRST, AND *covered, uncitable* IS A COMFORTABLE CONCLUSION
  // THAT ENDS THE SEARCH.** The criterion was two documents away the whole time.
  //
  // ---- WHAT THIS TEST ADDS THAT THE CLASSIFIER TEST CANNOT.
  //
  // `Impact_analysis_classifies_release_changes_and_security_blockers` calls `CatalogImpactAnalyzer.Analyze`
  // **directly** and asserts the six `CatalogImpactKind` values it returns. ***THAT IS THE REPORT, NOT THE
  // REFUSAL.*** The block lives one layer up, in `CatalogToolRunner`: any `SecuritySensitiveIncompatible` or
  // `RemovedProhibited` impact makes the tool exit **2**. **Measured before writing this: `RunAsync` is
  // asserted five times across `tests/`, against exit codes 0 and 1 — *NEVER 2*.** So the classifier could
  // have gone on naming the security case correctly while the release stopped being blocked, and every
  // existing assertion would have held.
  //
  // ⚠⚠ THE FIRST ASSERTION IS THE ANTI-VACUITY CONTROL AND IT IS NOT DECORATION: an identical baseline must
  // exit **0**. Without it, a runner that returned 2 unconditionally — or one that failed to load the
  // baseline and bailed — would satisfy the interesting half. *The pair is what makes the exit code mean
  // "this release is blocked" rather than "this tool returns 2".*
  //
  // ⚠⚠⚠ AND THE MUTATION IS CHOSEN TO REACH THE SENSITIVE ARM SPECIFICALLY RATHER THAN THE OTHER ONE.
  // `RemovedProhibited` also exits 2 and would be far easier to arrange — delete a resource from the
  // baseline — **but it would witness a DIFFERENT clause and leave the criterion's own word, *sensitive*,
  // asserted by nothing.** Flipping `textFormat` on a `SecuritySensitiveNonOverridable` resource changes its
  // compatibility fingerprint, so the analyzer reaches the classification branch that tests the security
  // flag. *The resource is selected BY ITS CLASSIFICATION, not by name, so the test follows the catalog if
  // the sensitive resources are ever renamed.*
  [Fact]
  [Trait("Criterion", "AC-LOC-0020")]
  public async Task Impact_blocks_security_sensitive_incompatibility_and_clears_an_unchanged_release()
  {
    var paths = GetPaths();
    var temporary = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(temporary);

    try
    {
      var baseline = Path.Combine(temporary, "baseline.json");
      File.Copy(paths.Manifest, baseline);

      Assert.Equal(0, await CatalogToolRunner.RunAsync([
        "impact", "--manifest", paths.Manifest, "--schema", paths.Schema, "--baseline", baseline]));

      var document = JsonNode.Parse(await File.ReadAllTextAsync(baseline))!;
      var sensitive = document["resources"]!.AsArray()
        .First(resource => resource!["securityClassification"]!.GetValue<string>()
          == nameof(LocalizationSecurityClassification.SecuritySensitiveNonOverridable));
      sensitive!["textFormat"] = nameof(LocalizationTextFormat.MultilineText);
      await File.WriteAllTextAsync(baseline, document.ToJsonString());

      Assert.Equal(2, await CatalogToolRunner.RunAsync([
        "impact", "--manifest", paths.Manifest, "--schema", paths.Schema, "--baseline", baseline]));
    }
    finally
    {
      Directory.Delete(temporary, true);
    }
  }

  // ==================================================================================================
  // ⚠⚠⚠ FOUR CLOSED SETS, EACH DECLARED TWICE IN TWO LANGUAGES, TIED TOGETHER BY NOTHING.
  // ==================================================================================================
  //
  // `AC-LOC-0028` opens *"Only Ordinary and SecuritySensitiveNonOverridable exist…"*. **That is a CLOSED-SET
  // claim, and this repository states it twice: once as a C# `enum` the compiler checks, and once as a JSON
  // Schema `enum` array the compiler has never heard of.** *Nothing reads one against the other.*
  //
  // ⚠ THE FAILURE IS SILENT IN BOTH DIRECTIONS AND NEITHER IS EXOTIC. Add a third C# member and the manifest
  // validator keeps rejecting it as schema-invalid — a value the domain accepts and the catalog cannot
  // express. Add a third SCHEMA value and manifests carrying it parse, then fail deserialisation at a layer
  // that has no idea a schema promised it. **Both changes are one line, both compile, and every existing
  // test stays green:** the schema is a raw string literal as far as the build is concerned, checked by
  // nothing until something is built that reads it.
  //
  // ⚠⚠ ALL FOUR SETS ARE CHECKED, NOT JUST THE CRITERION'S ONE. `securityClassification` and `textFormat`
  // are both inputs to `CompatibilityFingerprint.Calculate`, so drift in either silently changes what
  // "compatible" means for every tenant override — the criterion names one and the mechanism has two.
  // *`lifecycle` and `category` come along because the cost of a fifth line is nothing and the cost of
  // discovering the gap again is what this comment cost.*
  //
  // ⚠⚠⚠ SET EQUALITY, DELIBERATELY, NOT SEQUENCE EQUALITY. These values cross the boundary AS STRINGS, so
  // reordering either declaration is semantically inert. **An ordered assertion would redden on a harmless
  // tidy of the schema, and this file already documents where that leads — a failure whose obvious remedy is
  // to re-baseline it teaches the reader to silence the alarm.** *Sorted comparison is the claim that is
  // actually true: the same NAMES exist on both sides.*
  //
  // ---- THE OTHER TWO CLAUSES OF `AC-LOC-0028` ARE NOT CARRIED HERE AND ARE NOT CARRIED ANYWHERE I FOUND.
  //
  // *"non-overridable mutation fails"* IS covered — `LocalizationDomainTests.Security_sensitive_resource_
  // cannot_create_override`, cited there to `AC-LOC-0009`.
  // ⚠ *"non-overridable PREVIEW fails"* IS NOT. `LocalizationPreviewTests` has two tests: a placeholder
  // accept/reject pair over an ORDINARY resource, and a suspended-tenant refusal. **Neither previews a
  // `SecuritySensitiveNonOverridable` resource**, so a preview handler that happily previewed one would be
  // caught by nothing. Recorded rather than fixed: it is a new fixture case, not a missing assertion.
  [Fact]
  [Trait("Criterion", "AC-LOC-0028")]
  public void Schema_closed_sets_match_the_domain_enums()
  {
    var schema = JsonNode.Parse(File.ReadAllText(GetPaths().Schema))!;
    var properties = schema["$defs"]!["resource"]!["properties"]!;

    (string Property, string[] Names)[] pairs =
    [
      ("securityClassification", Enum.GetNames<LocalizationSecurityClassification>()),
      ("textFormat", Enum.GetNames<LocalizationTextFormat>()),
      ("lifecycle", Enum.GetNames<LocalizationResourceLifecycle>()),
      ("category", Enum.GetNames<LocalizationResourceCategory>())
    ];

    foreach (var (property, names) in pairs)
    {
      var declared = properties[property]!["enum"]!.AsArray()
        .Select(value => value!.GetValue<string>())
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

      Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal).ToArray(), declared);
    }
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
