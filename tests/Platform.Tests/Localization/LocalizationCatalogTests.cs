using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Generated;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationCatalogTests
{
  // ⚠⚠⚠ EXAMINED AND UNCITED, AND THE EXAMINATION FOUND AN UNCOVERED CRITERION CLAUSE — THE LARGEST IN
  // THIS PASS. The ordering assertion below looks like `AC-LOC-0002`, *"Manifest validation rejects
  // duplicate/REORDERED, invalid, renamed, or reused retired ResourceKeys…"*. **IT IS ADJACENT-VERB.** This
  // asserts the generated catalog **IS** ordered; the criterion says validation **REJECTS** a reordered
  // manifest. A postcondition of the good path is not a refusal of the bad one.
  //
  // ⚠⚠ AN ADJACENT-VERB DISPOSAL PREDICTS WHERE THE REAL VERB LIVES, SO IT WAS SEARCHED FOR. **MISS —
  // NOTHING EXERCISES THAT CLAUSE ANYWHERE.** Two mechanism searches, both completable:
  //
  //   `CatalogToolRunner.RunAsync` — the only way to invoke the tool — occurs FIVE times across `tests/`,
  //   all in `LocalizationCatalogToolTests`, and `validate` is among them EXACTLY ONCE: on the checked-in
  //   manifest, expecting 0.
  //   `SemanticCatalogValidator` — the only validation entry point in the tool — is referenced by NO TEST.
  //
  // **So all five named rejections — duplicate, reordered, invalid, renamed, reused-retired — have no
  // test.** Searched by ENTRY POINT rather than by name, because a name search over "validate" cannot be
  // complete and a call-site search over one runner can.
  //
  // ⚠ Everything this test does assert is a REPOSITORY FACT rather than a criterion: six resources, schema
  // and catalog version 1, every resource at version 1 with both defaults non-empty. Those are worth having
  // and there is nothing to cite them to. The 32-byte fingerprint checks are the SOURCE side of
  // `AC-LOC-0029`'s *"exactly 32 PERSISTED bytes"*, which is cited where persistence happens
  // (`PlatformLocalizationSqlServerTests`) and where the hash is computed (`LocalizationPrimitiveTests`) —
  // citing it here as well would widen *persisted* to *anywhere the bytes appear*.
  [Fact]
  public void Generated_catalog_contains_the_six_approved_resources()
  {
    var catalog = GeneratedLocalizationCatalog.Instance;

    Assert.Equal(1, catalog.CatalogSchemaVersion.Value);
    Assert.Equal(1, catalog.CatalogVersion.Value);
    Assert.Equal(6, catalog.Resources.Count);
    Assert.Equal(catalog.Resources.OrderBy(resource => resource.ResourceKey.Value, StringComparer.Ordinal), catalog.Resources);
    Assert.All(catalog.Resources, resource =>
    {
      Assert.Equal(1, resource.ResourceVersion.Value);
      Assert.NotEmpty(resource.EnglishDefault);
      Assert.NotEmpty(resource.ArabicDefault);
      Assert.Equal(32, resource.PlaceholderFingerprint.Bytes.Length);
      Assert.Equal(32, resource.CompatibilityFingerprint.Bytes.Length);
    });
  }

  [Fact]
  public void Authentication_resources_are_non_overridable_and_generic()
  {
    var resources = GeneratedLocalizationCatalog.Instance.Resources
      .Where(resource => resource.ResourceKey.Value.StartsWith("platform.authentication.", StringComparison.Ordinal));

    Assert.Equal(2, resources.Count());
    Assert.All(resources, resource =>
    {
      Assert.False(resource.TenantOverridable);
      Assert.Equal(LocalizationSecurityClassification.SecuritySensitiveNonOverridable, resource.SecurityClassification);
    });
  }

  // ⚠ CITES THE SECOND CLAUSE OF `AC-LOC-0005` — *"…and NEUTRAL PRODUCTION OUTPUT NEVER EXPOSES
  // ResourceKey."* `LocalizationResolverTests` carries the four-step chain and its source/culture
  // reporting; this carries the neutral string itself, in both cultures, at the catalog layer where it is
  // defined. **Scope: these are the catalog's constants, not an end-to-end Production response** — what is
  // shown is that the value a caller falls back to contains no key, not that every path reaches it.
  //
  // ⚠⚠⚠ AND THE THIRD ASSERTION IS EMPHASIS, NOT A CHECK. `Assert.DoesNotContain("platform.", …)` **cannot
  // fail in any state where the assertion two lines above it passes**: that one pins the value to exactly
  // `"Text unavailable"`, and a fixed string either contains `"platform."` or does not, decided already.
  //
  // The test worth applying to any suspicious assertion is a single question — *could this fail in any
  // state where every other assertion in the test passes?* — and here the answer is no. **Kept and
  // labelled rather than deleted: it states the SECURITY INTENT of the constant, which the equality does
  // not, and a reader who removes it loses the only line saying why that string was chosen.**
  [Fact]
  [Trait("Criterion", "AC-LOC-0005")]
  public void Neutral_fallbacks_do_not_disclose_missing_keys()
  {
    var catalog = GeneratedLocalizationCatalog.Instance;

    Assert.Equal("Text unavailable", catalog.GetNeutralFallback(LocalizationCulture.English));
    Assert.Equal("النص غير متاح", catalog.GetNeutralFallback(LocalizationCulture.Arabic));
    Assert.DoesNotContain("platform.", catalog.GetNeutralFallback(LocalizationCulture.English), StringComparison.Ordinal);
  }
}
