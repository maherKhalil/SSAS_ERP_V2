using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;

namespace SSAS.Localization.CatalogTool;

internal static class CatalogImpactAnalyzer
{
  public static IReadOnlyList<CatalogImpact> Analyze(CatalogValidationResult baseline, CatalogValidationResult candidate)
  {
    var previous = baseline.Resources.ToDictionary(resource => resource.ResourceKey.Value, StringComparer.Ordinal);
    var current = candidate.Resources.ToDictionary(resource => resource.ResourceKey.Value, StringComparer.Ordinal);
    var impacts = new List<CatalogImpact>();

    foreach (var resource in candidate.Resources)
    {
      if (!previous.TryGetValue(resource.ResourceKey.Value, out var old))
      {
        impacts.Add(new(resource.ResourceKey.Value, CatalogImpactKind.Added));
        continue;
      }

      if (old.Lifecycle == LocalizationResourceLifecycle.Active && resource.Lifecycle == LocalizationResourceLifecycle.Retired)
      {
        impacts.Add(new(resource.ResourceKey.Value, CatalogImpactKind.Retired));
        continue;
      }

      if (ResourceEqual(old, resource))
      {
        continue;
      }

      if (old.CompatibilityFingerprint.Equals(resource.CompatibilityFingerprint))
      {
        impacts.Add(new(resource.ResourceKey.Value, CatalogImpactKind.ChangedCompatible));
      }
      else if (old.SecurityClassification == LocalizationSecurityClassification.SecuritySensitiveNonOverridable ||
        resource.SecurityClassification == LocalizationSecurityClassification.SecuritySensitiveNonOverridable)
      {
        impacts.Add(new(resource.ResourceKey.Value, CatalogImpactKind.SecuritySensitiveIncompatible));
      }
      else
      {
        impacts.Add(new(resource.ResourceKey.Value, CatalogImpactKind.ChangedIncompatible));
      }
    }

    foreach (var removed in previous.Keys.Except(current.Keys, StringComparer.Ordinal))
    {
      impacts.Add(new(removed, CatalogImpactKind.RemovedProhibited));
    }

    return impacts.OrderBy(impact => impact.ResourceKey, StringComparer.Ordinal).ToArray();
  }

  private static bool ResourceEqual(LocalizationResourceDefinition left, LocalizationResourceDefinition right) =>
    left.ResourceVersion == right.ResourceVersion &&
    left.Lifecycle == right.Lifecycle &&
    string.Equals(left.EnglishDefault, right.EnglishDefault, StringComparison.Ordinal) &&
    string.Equals(left.ArabicDefault, right.ArabicDefault, StringComparison.Ordinal) &&
    left.CompatibilityFingerprint.Equals(right.CompatibilityFingerprint);
}

internal sealed record CatalogImpact(string ResourceKey, CatalogImpactKind Kind);

internal enum CatalogImpactKind
{
  Added,
  ChangedCompatible,
  ChangedIncompatible,
  Retired,
  SecuritySensitiveIncompatible,
  RemovedProhibited
}
