using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Generated;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationCatalogTests
{
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

  [Fact]
  public void Neutral_fallbacks_do_not_disclose_missing_keys()
  {
    var catalog = GeneratedLocalizationCatalog.Instance;

    Assert.Equal("Text unavailable", catalog.GetNeutralFallback(LocalizationCulture.English));
    Assert.Equal("النص غير متاح", catalog.GetNeutralFallback(LocalizationCulture.Arabic));
    Assert.DoesNotContain("platform.", catalog.GetNeutralFallback(LocalizationCulture.English), StringComparison.Ordinal);
  }
}
