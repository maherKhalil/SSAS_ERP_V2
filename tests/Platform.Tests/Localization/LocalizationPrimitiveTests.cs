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
  [InlineData("{name}", "name")]
  [InlineData("{a}{z}{a}", "a,z")]
  [InlineData("{{literal}} {amount}", "amount")]
  [InlineData("مرحبا {userName}", "userName")]
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

  [Fact]
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

  [Fact]
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
