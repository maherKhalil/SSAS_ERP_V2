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
  //   `{a}{z}{a}` → `"a,z"`     REPETITION — `a` appears twice and the set holds it once
  //   `{z}{a}` → `"a,z"`        SET ORDERING, and it is the only row that can see it — see below
  //   `{{literal}} {amount}`    ESCAPING — `{{` is a literal brace and yields no name
  //   `مرحبا {userName}`        the surrounding text is non-ASCII and the name's CASE survives intact
  //
  // ⚠⚠⚠ THE `{z}{a}` ROW WAS ADDED BECAUSE THE FIXTURE WAS SHORT OF ITS OWN CITATION, AND A PLANT PROVED
  // IT. `{a}{z}{a}` **is already in sorted order**, so insertion order and sorted order both yield
  // `"a,z"` — the row was cited for SET and could only ever see the DISTINCT half of it. Removing
  // `.Order(…)` from `PlaceholderSet` left every row here green.
  //
  // **`{z}{a}` is the smallest fixture that fails when the sort is removed**, and it is the difference
  // between this theory carrying *set* and merely appearing to. ⚠ *AN ARRANGEMENT CAN BE SHORT OF ITS
  // CITATION IN A WAY NO ASSERTION REVEALS* — nothing about the old rows looked incomplete, and the
  // expected value `"a,z"` is identical either way.
  //
  // ⚠⚠ RE-PLANTED AFTER ADDING IT, BECAUSE THE EARLIER PLANT CERTIFIED A DIFFERENT FIXTURE. Removing
  // `.Order(…)` again now fails **TWO** tests where it previously failed one:
  //
  //   `Parser_accepts_exact_valid_grammar(text: "{z}{a}", expected: "a,z")`   the new row
  //   `Placeholder_fingerprint_uses_sorted_distinct_lf_utf8_sha256`           the pre-existing alarm
  //
  // **AND THE DIFFERENCE BETWEEN THE TWO FAILURE TEXTS IS THE WHOLE POINT OF ADDING THE ROW.** The
  // fingerprint reports *Collections differ, Expected [100, 201, 33…] Actual [98, 240, 121…]* — bytes, with
  // no mention of ordering, which invites re-baselining. **This row reports its own subject: the text
  // `{z}{a}` and the expected `a,z`.** A misdescribed alarm now has a correctly-described companion that
  // fails first alphabetically and says what actually broke.
  //
  // `Parser_rejects_malformed_tokens` carries MALFORMED over seven shapes, and
  // `Formatter_requires_exact_names_and_does_not_reparse_values` carries MISSING — `Format` with an empty
  // value map fails.
  //
  // ⚠⚠ UNKNOWN IS NOT HERE. It is `LocalizationResolverTests.Batch_limits_and_placeholder_contracts_are_
  // enforced`, which passes an extra `["other"]` and gets `PlaceholderMismatch`. **Cited there is not cited
  // here**, and the criterion needs both files.
  //
  // ⚠⚠⚠ AND *ACCEPT REORDER* IS THE ONE PROPERTY NOTHING STATES, THOUGH THE MECHANISM FOR IT IS ASSERTED.
  // Sorting the set is what makes order irrelevant — `{a}{z}{a}` yields `a,z` and so would `{z}{a}` — and
  // `Placeholder_fingerprint_uses_sorted_distinct_lf_utf8_sha256` below fingerprints the SORTED DISTINCT
  // set for exactly that reason. **But no test parses two texts that differ ONLY in placeholder order and
  // shows them equal or compatible.**
  //
  // ⚠⚠ PLANTED, BECAUSE MY FIRST VERSION OF THIS PARAGRAPH PREDICTED THE BLAST RADIUS INSTEAD OF MEASURING
  // IT — AND THE PREDICTION WAS WRONG. I wrote that removing the sort *would leave every row above green*.
  // Removing `.Order(StringComparer.Ordinal)` from `PlaceholderSet.cs:11` in `src/`, reverted afterwards:
  //
  //   **ONE test reddened — `Placeholder_fingerprint_uses_sorted_distinct_lf_utf8_sha256`**, with
  //   *Assert.Equal() Failure: Collections differ, Expected: [100, 201, 33, …] Actual: [98, 240, 121, …]*.
  //   Every row of THIS theory stayed green, and so did the other 1,104 Platform tests.
  //
  // ⚠ THE ROWS SURVIVED FOR A REASON THAT INDICTS THE ARRANGEMENT: **`{a}{z}{a}` is already in sorted
  // order**, so insertion order and sorted order produce the same `"a,z"`. The one row that could have
  // discriminated would need a name out of order — `{z}{a}` — and no row here has one.
  //
  // **SO THE ALARM EXISTS AND NAMES THE WRONG THING.** A change that breaks reorder acceptance is caught,
  // but it is reported as *this fingerprint's bytes moved* — true, and about the hash rather than about the
  // property. Whoever meets that failure re-baselines the expected bytes and the real consequence goes
  // unnoticed. ***A PROPERTY GUARDED ONLY BY THE ASSERTED IMPLEMENTATION OF ITS MECHANISM is not
  // unguarded; it is guarded by an alarm that describes the mechanism and not the property.***
  [InlineData("{name}", "name")]
  [InlineData("{a}{z}{a}", "a,z")]
  [InlineData("{z}{a}", "a,z")]
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
  //
  // ⚠⚠⚠ AND THERE IS A SECOND CRITERION ABOUT THIS EXACT LINE, UNCITED, WHICH EXPLAINS WHY THE EMOJI IS
  // HERE AT ALL. `AC-LOC-0058` — *"Values AT AND ABOVE 512/4000 UTF-16 limits, INCLUDING BOUNDARY SURROGATE
  // PAIRS, behave IDENTICALLY IN DOMAIN/API/SQL."* **Someone chose a surrogate pair at the boundary on
  // purpose, and the criterion that asked for it is not the one cited above.**
  //
  // ⚠ IT IS DELIBERATELY NOT CITED HERE, because it is a THREE-LAYER EQUIVALENCE CLAIM — *identically in
  // Domain/API/SQL* — and this file is one layer. A domain-only test showing the boundary behaves correctly
  // says nothing about whether the API and SQL agree with it, and agreement is the whole content of that
  // sentence. **Citing it here would be the adjacent-scope defect: right subject, right fixture, wrong
  // arity of claim.**
  //
  // ⚠⚠ AND IT INHERITS THE SAME HALF-COVERED PAIR, ONE LEVEL UP: `0058` also says *512/4000*, so even its
  // domain leg is only half-exercised. **Two criteria state the same compound literal and neither has a
  // 4000 boundary anywhere in the repository.**
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
