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
  // **THE EXACT CLAIM, WHICH IS NARROWER AND STRONGER THAN *the refusal paths are never entered*: the
  // validator IS entered — once, on the CHECKED-IN manifest, expecting 0. NO INPUT THAT SHOULD BE REJECTED
  // IS EVER PASSED TO IT.** That forecloses the obvious rebuttal, and *never entered* would have been false.
  //
  // ⚠ HOW MANY REJECTIONS ARE NAMED IS AMBIGUOUS IN THE CRITERION ITSELF, SO IT IS LEFT AMBIGUOUS HERE:
  // *duplicate/reordered, invalid, renamed, or reused retired* is **FOUR comma-groups, the first a
  // slash-pair — four listed groups, five rejections if that pair splits.** The sentence does not say, and
  // resolving it silently would be inventing a denominator. None of them is covered either way.
  //
  // Searched by ENTRY POINT rather than by name, because a name search over "validate" cannot be complete
  // and a call-site search over one runner can. ⚠⚠ **That is also why this MISS is a strong result rather
  // than a weak one: a miss confirms weakly when the query is shaped by the verb you disposed on, and
  // strongly when it is an entry-point enumeration over a completable mechanism — the emptiness is not
  // *I looked for the wrong thing*, it is THERE IS EXACTLY ONE DOOR AND NOBODY WENT THROUGH IT.**
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

  // ⚠ CITES `AC-LOC-0009` — *"All protected causes retain ONE GENERIC code/ResourceKey and CANNOT BE
  // TENANT-OVERRIDDEN."* — AS THE CATALOG HALF OF A TWO-SITE PAIR.
  //
  // **CLAUSE 1 IS `Assert.Equal(2, resources.Count())`, and it is the assertion most likely to be read as
  // bookkeeping.** *One generic code* means the authentication surface maps every failure cause onto a
  // small fixed set of keys rather than one key per cause — the catalog's own description of
  // `authentication_failed` is *"Generic non-enumerating authentication failure."* **A build that added a
  // key per cause would be enumerating exactly what the criterion forbids, and this count is the only line
  // that would object.**
  //
  // ⚠⚠ CLAUSE 2 IS SPLIT ACROSS TWO FILES AND THIS IS THE FLAG HALF. `TenantOverridable` false plus the
  // `SecuritySensitiveNonOverridable` classification are DECLARATIONS; `LocalizationDomainTests.Security_
  // sensitive_resource_cannot_create_override` is the BEHAVIOUR that reads them. **A flag nothing consults
  // satisfies this end; a hard-coded refusal ignoring the flag satisfies that end.** Two independent
  // failure modes, so the pair is disjointness rather than redundancy and neither citation is duplicate.
  //
  // ⚠ ANTI-VACUITY IS THE COUNT ITSELF, WHICH IS UNUSUAL AND WORTH NAMING: `resources` is a filtered walk,
  // and a filter that matched nothing would fail `Assert.Equal(2, …)` before reaching the `Assert.All`.
  // **The population floor and the criterion's first clause are the same line here** — no separate floor is
  // needed, and removing the count to "simplify" would silently make the two bans below vacuous.
  //
  // ⚠⚠ THIS IS THE INVERSE OF THE USUAL WARNING AND NOTHING ABOUT THE LINE'S SHAPE SAYS SO. *A floor that
  // binds nothing is insurance* — the standing advice is to report the bound rather than the value.
  // **HERE THE FLOOR *IS* THE CLAUSE.** `Assert.Equal(2, …)` looks like bookkeeping and is the only
  // assertion in the repository that would object to a build adding one authentication key per failure
  // cause, which is precisely the enumeration `AC-LOC-0009` forbids. **Deleting it as a simplification
  // removes a criterion clause AND the population guard in a single edit**, and a reviewer would see a
  // tidy-up.
  [Fact]
  [Trait("Criterion", "AC-LOC-0009")]
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
