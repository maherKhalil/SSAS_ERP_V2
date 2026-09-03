using System.Security.Cryptography;
using System.Text;
using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationPrimitiveTests
{
  [Fact]
  public void Cultures_are_exact_and_derive_direction()
  {
    Assert.Equal(TextDirection.Ltr, LocalizationCulture.Create("en").Value.Direction);
    Assert.Equal(TextDirection.Rtl, LocalizationCulture.Create("ar").Value.Direction);
    Assert.True(LocalizationCulture.Create("EN").IsFailure);
  }

  [Fact]
  public void Resource_key_enforces_approved_ordinal_shape_and_limit()
  {
    Assert.True(ResourceKey.Create("platform.common.actions.save").IsSuccess);
    Assert.True(ResourceKey.Create("Platform.common.actions.save").IsFailure);
    Assert.True(ResourceKey.Create(new string('a', ResourceKey.MaximumLength + 1)).IsFailure);
  }

  [Theory]
  // ⚠ CITES `AC-LOC-0007` — *"Exact PARSER/ESCAPING/REPETITION/CASE/SET rules ACCEPT REORDER and reject
  // every MISSING/UNKNOWN/MALFORMED placeholder."* Eight named properties in one sentence, so each row is
  // matched to the one it carries rather than the theory being cited as a block:
  //
  //   `{name}`                  parser, the base case
  //   `{a}{z}{a}` → `"a,z"`     REPETITION (`a` twice) and SET (distinct, and sorted — `z` follows `a`)
  //   `{{literal}} {amount}`    ESCAPING — `{{` is a literal brace and yields no name
  //   `مرحبا {userName}`        the surrounding text is non-ASCII and the name's CASE survives intact
  //
  // `Parser_rejects_malformed_tokens` carries MALFORMED over seven shapes, and
  // `Formatter_requires_exact_names_and_does_not_reparse_values` carries MISSING — `Format` with an empty
  // value map fails.
  //
  // ⚠⚠ UNKNOWN IS NOT HERE. It is `LocalizationResolverTests.Batch_limits_and_placeholder_contracts_are_
  // enforced`, which passes an extra `["other"]` and gets `PlaceholderMismatch`. **Cited there is not cited
  // here**, and the criterion needs both files.
  //
  // ⚠⚠⚠ AND *ACCEPT REORDER* IS THE ONE PROPERTY NOTHING ASSERTS, THOUGH THE MECHANISM FOR IT IS VISIBLE.
  // Sorting the set is what makes order irrelevant — `{a}{z}{a}` yields `a,z` and so would `{z}{a}` — and
  // `Placeholder_fingerprint_uses_sorted_distinct_lf_utf8_sha256` below fingerprints the SORTED DISTINCT
  // set for exactly that reason. **But no test parses two texts that differ ONLY in placeholder order and
  // shows them equal or compatible.** The property is a consequence of an implementation detail that is
  // itself asserted; it is not asserted directly, and a change from sorted to insertion order would break
  // reorder acceptance while leaving every row above green.
  [InlineData("{name}", "name")]
  [InlineData("{a}{z}{a}", "a,z")]
  [InlineData("{{literal}} {amount}", "amount")]
  [InlineData("مرحبا {userName}", "userName")]
  [Trait("Criterion", "AC-LOC-0007")]
  public void Parser_accepts_exact_valid_grammar(string text, string expected)
  {
    var result = LocalizationPlaceholderParser.Parse(text);

    Assert.True(result.IsSuccess);
    Assert.Equal(expected, string.Join(',', result.Value.Names));
  }

  [Theory]
  [InlineData("{")]
  [InlineData("}")]
  [InlineData("{}")]
  [InlineData("{ name }")]
  [InlineData("{a-b}")]
  [InlineData("{{{")]
  [InlineData("{a{b}}")]
  public void Parser_rejects_malformed_tokens(string text)
  {
    Assert.True(LocalizationPlaceholderParser.Parse(text).IsFailure);
  }

  [Fact]
  public void Formatter_requires_exact_names_and_does_not_reparse_values()
  {
    var placeholders = LocalizationPlaceholderParser.Parse("Hello {name}").Value;
    var result = LocalizationPlaceholderParser.Format(
      "Hello {name}",
      placeholders,
      new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "{other}<b>" });

    Assert.Equal("Hello {other}<b>", result.Value);
    Assert.True(LocalizationPlaceholderParser.Format("Hello {name}", placeholders, new Dictionary<string, string>()).IsFailure);
  }

  // ⚠ CITES THE DETERMINISM HALF OF `AC-LOC-0029` — *"Canonical examples produce DETERMINISTIC SHA-256 and
  // exactly 32 persisted bytes."*
  //
  // **The determinism is shown by RECOMPUTING the hash independently from the canonical form** — sorted,
  // distinct, LF-joined, UTF-8 — rather than by calling `Calculate` twice and comparing. ⚠⚠ THAT
  // DISTINCTION IS THE WHOLE VALUE: two calls agreeing proves only that the function is a function, and
  // would still pass if the canonicalisation were wrong in the same way both times. Deriving `a\nz` from
  // `["z", "a", "z"]` by hand is what pins the canonical form itself.
  //
  // ⚠ IT DOES NOT COVER *exactly 32 PERSISTED bytes* — that half is
  // `PlatformLocalizationSqlServerTests.Aggregate_and_history_enforce_coherence_uniqueness_fingerprints_
  // and_immutability`, which reads the length back out of SQL Server. **`Compatibility_fingerprint_…:93`
  // asserts 32 bytes IN MEMORY, and an in-memory 32 is not a persisted 32** — a column could truncate,
  // widen or store a different encoding without any of that showing here.
  [Fact]
  [Trait("Criterion", "AC-LOC-0029")]
  public void Placeholder_fingerprint_uses_sorted_distinct_lf_utf8_sha256()
  {
    var set = PlaceholderSet.Create(["z", "a", "z"]).Value;
    var expected = SHA256.HashData(Encoding.UTF8.GetBytes("a\nz"));

    Assert.Equal(expected, PlaceholderFingerprint.Calculate(set).Bytes);
    Assert.Equal(SHA256.HashData([]), PlaceholderFingerprint.Calculate(PlaceholderSet.Create([]).Value).Bytes);
  }

  [Fact]
  public void Compatibility_fingerprint_ignores_wording_and_changes_with_policy()
  {
    var key = ResourceKey.Create("platform.common.validation.required").Value;
    var placeholders = PlaceholderSet.Create(["fieldName"]).Value;
    var first = CompatibilityFingerprint.Calculate(
      key,
      LocalizationTextFormat.PlainText,
      LocalizationSecurityClassification.Ordinary,
      true,
      placeholders);
    var second = CompatibilityFingerprint.Calculate(
      key,
      LocalizationTextFormat.PlainText,
      LocalizationSecurityClassification.Ordinary,
      false,
      placeholders);

    Assert.False(first.Equals(second));
    Assert.Equal(32, first.Bytes.Length);
  }

  // ⚠ CITES `AC-LOC-0008` FOR THREE OF ITS FOUR CLAUSES — *"PlainText/MultilineText enforce EXACT CONTROL
  // and 512/4000 UTF-16 LIMITS BEFORE PERSISTENCE and use FORMAT-MATCHING `nvarchar(512)`/`nvarchar(4000)`
  // COLUMNS while PRESERVING VALID TEXT."*
  //
  // ⚠⚠ AND THE EMOJI IS THE WHOLE POINT OF THE FIRST LINE, WHICH READS LIKE ARBITRARY PADDING.
  // `new string('x', 510) + 😀` is **511 CODEPOINTS AND 512 UTF-16 UNITS** — the emoji is a surrogate pair.
  // So this row accepts at exactly 512 UTF-16 and the next rejects at 513. **A limit counting CODEPOINTS
  // would see 511 here, accept the `+ "x"` row, and fail only this test** — which is what makes the
  // criterion's words *UTF-16 LIMITS* observable rather than decorative. Replace the emoji with an `x` and
  // the test still passes while checking a different property.
  //
  // *Exact control* is the two rows below it: `"a\nb"` is refused as PlainText and `"a\r\nb\tc"` is
  // accepted as MultilineText, so the rule is per-FORMAT and not global. *Preserving valid text* is the
  // `Assert.Equal` on that same line — the accepted value comes back byte-for-byte rather than normalised.
  //
  // ⚠⚠⚠ TWO RESIDUALS, AND THE FIRST IS HALF OF A NUMBER PAIR THE CRITERION STATES AS ONE.
  //
  //   **4000 IS ASSERTED BY NOTHING.** The criterion says *512/4000*; only 512 has a boundary here, and
  //   `MultilineText` is exercised for CONTROL rules and never for LENGTH. Searched: `4000` across `tests/`
  //   returns JWT guids, payroll amounts and an sqlcmd constant — **no localization length assertion at
  //   all.** A `MultilineText` limit wrong by any amount would be caught by nothing.
  //
  //   *Format-matching `nvarchar(512)`/`nvarchar(4000)` columns* is a SCHEMA claim and cannot be made
  //   here — these are value-object constructions. It belongs to the EF configuration and its SQL tests.
  //
  // **So the citation is for the domain half of a criterion whose sentence spans domain and schema**, and
  // the numeric pair it states is half-covered.
  [Fact]
  [Trait("Criterion", "AC-LOC-0008")]
  public void Text_validation_uses_utf16_boundaries_and_preserves_input()
  {
    var plain = new string('x', 510) + char.ConvertFromUtf32(0x1F600);
    Assert.True(LocalizationText.Create(plain, LocalizationTextFormat.PlainText).IsSuccess);
    Assert.True(LocalizationText.Create(plain + "x", LocalizationTextFormat.PlainText).IsFailure);
    Assert.True(LocalizationText.Create("a\nb", LocalizationTextFormat.PlainText).IsFailure);
    Assert.Equal("a\r\nb\tc", LocalizationText.Create("a\r\nb\tc", LocalizationTextFormat.MultilineText).Value.Value);
  }

  [Fact]
  public void Text_validation_rejects_unpaired_surrogates_and_prohibited_controls()
  {
    Assert.True(LocalizationText.Create("x\uD800", LocalizationTextFormat.PlainText).IsFailure);
    Assert.True(LocalizationText.Create("x\0", LocalizationTextFormat.MultilineText).IsFailure);
    Assert.True(LocalizationText.Create("x\u0001", LocalizationTextFormat.MultilineText).IsFailure);
  }

  [Fact]
  public void Positive_versions_do_not_wrap()
  {
    Assert.True(CatalogVersion.Create(0).IsFailure);
    Assert.True(CatalogVersion.Create(long.MaxValue).Value.Increment().IsFailure);
    Assert.True(ResourceVersion.Create(int.MaxValue).Value.Increment().IsFailure);
    Assert.True(TenantLocalizationVersion.Create(long.MaxValue).Value.Increment().IsFailure);
    Assert.True(TenantOverrideVersion.Create(long.MaxValue).Value.Increment().IsFailure);
  }
}
