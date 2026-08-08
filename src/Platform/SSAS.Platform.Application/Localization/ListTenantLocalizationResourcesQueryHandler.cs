using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain.Localization;
using LocalizationDomainErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

public sealed class ListTenantLocalizationResourcesQueryHandler(
  ILocalizationCatalog catalog,
  ITenantLocalizationAdministrationReadService administrationReadService,
  ILocalizationTextResolver resolver,
  IRequestTenantEligibility eligibility,
  ICurrentTenant currentTenant)
{
  public async Task<Result<PagedResult<LocalizationAdministrationResource>>> HandleAsync(
    ListTenantLocalizationResourcesQuery query,
    CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || query.PageNumber < 1 || query.PageSize is < 1 or > 100)
      return Result.Failure<PagedResult<LocalizationAdministrationResource>>(new Error("request.invalid", "The localization request is invalid."));
    var culture = LocalizationCulture.Create(query.Culture);
    if (culture.IsFailure) return Result.Failure<PagedResult<LocalizationAdministrationResource>>(culture.Error);
    var tenantEligibility = await eligibility.GetEligibilityAsync(tenantId, cancellationToken);
    if (!tenantEligibility.IsAuthenticationEligible) return Result.Failure<PagedResult<LocalizationAdministrationResource>>(LocalizationDomainErrors.TenantIneligible);

    if (query.Lifecycle is not null and not "Active" and not "Retired" and not "All")
      return Result.Failure<PagedResult<LocalizationAdministrationResource>>(new Error("request.invalid", "The localization request is invalid."));
    var candidates = Filter(catalog.Resources, query).OrderBy(item => item.ResourceKey.Value, StringComparer.Ordinal).ToArray();
    var candidateOverrides = new Dictionary<string, TenantLocalizationOverrideAdministrationReadModel>(StringComparer.Ordinal);
    if (query.OverriddenOnly || query.IncompatibleOnly)
    {
      candidateOverrides = (await administrationReadService.ReadAsync(tenantId, culture.Value, candidates.Select(item => item.ResourceKey).ToArray(), cancellationToken))
        .ToDictionary(item => item.ResourceKey, StringComparer.Ordinal);
      candidates = candidates.Where(item => candidateOverrides.TryGetValue(item.ResourceKey.Value, out var value) &&
        (!query.IncompatibleOnly || !IsCompatible(value, item))).ToArray();
    }

    var totalCount = candidates.Length;
    var definitions = candidates.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArray();
    var overrides = definitions.Length == 0 ? [] : await administrationReadService.ReadAsync(
      tenantId, culture.Value, definitions.Select(item => item.ResourceKey).ToArray(), cancellationToken);
    var overridesByKey = overrides.ToDictionary(item => item.ResourceKey, StringComparer.Ordinal);
    var resolved = definitions.Length == 0 ? Result.Success<IReadOnlyList<EffectiveLocalizedText>>([]) : await resolver.ResolveTemplateExplicitBatchAsync(
      new LocalizationExplicitBatchRequest(definitions.Select(item => item.ResourceKey.Value).ToArray(), culture.Value.Value), cancellationToken);
    if (resolved.IsFailure) return Result.Failure<PagedResult<LocalizationAdministrationResource>>(resolved.Error);
    var effectiveByKey = resolved.Value.ToDictionary(item => item.ResourceKey.Value, StringComparer.Ordinal);
    var items = definitions.Select(definition => Map(definition, culture.Value, overridesByKey.GetValueOrDefault(definition.ResourceKey.Value), effectiveByKey[definition.ResourceKey.Value], catalog.CatalogVersion.Value)).ToArray();
    return Result.Success(new PagedResult<LocalizationAdministrationResource>(items, query.PageNumber, query.PageSize, totalCount));
  }

  internal static bool IsCompatible(TenantLocalizationOverrideAdministrationReadModel value, LocalizationResourceDefinition definition) =>
    definition.IsTenantEditable && value.PlaceholderFingerprint.AsSpan().SequenceEqual(definition.PlaceholderFingerprint.Bytes) &&
    value.CompatibilityFingerprint.AsSpan().SequenceEqual(definition.CompatibilityFingerprint.Bytes);

  internal static LocalizationAdministrationResource Map(LocalizationResourceDefinition definition, LocalizationCulture culture,
    TenantLocalizationOverrideAdministrationReadModel? value, EffectiveLocalizedText effective, long catalogVersion) => new(
      definition.ResourceKey.Value, definition.Module, definition.Group, definition.Category.ToString(), definition.TextFormat.ToString(),
      definition.Lifecycle.ToString(), definition.SecurityClassification.ToString(), definition.TenantOverridable, definition.ResourceVersion.Value,
      catalogVersion, culture.Value, definition.GetDefault(culture), effective.Text, value?.Value, value?.IsActive,
      value is null || IsCompatible(value, definition), value?.TenantOverrideVersion, value?.CurrentVersionNumber, value?.RowVersion,
      value?.ModifiedUtc, definition.Placeholders.Names, value?.EligibleUndoTargetVersion);

  private static IEnumerable<LocalizationResourceDefinition> Filter(IReadOnlyList<LocalizationResourceDefinition> definitions,
    ListTenantLocalizationResourcesQuery query)
  {
    var lifecycle = query.Lifecycle ?? "Active";
    return definitions.Where(item =>
      (lifecycle == "All" || item.Lifecycle.ToString() == lifecycle) &&
      (query.Module is null || string.Equals(item.Module, query.Module, StringComparison.Ordinal)) &&
      (query.Group is null || string.Equals(item.Group, query.Group, StringComparison.Ordinal)) &&
      (query.Category is null || string.Equals(item.Category.ToString(), query.Category, StringComparison.Ordinal)) &&
      (query.SecurityClassification is null || string.Equals(item.SecurityClassification.ToString(), query.SecurityClassification, StringComparison.Ordinal)) &&
      (string.IsNullOrEmpty(query.Search) || item.ResourceKey.Value.Contains(query.Search, StringComparison.OrdinalIgnoreCase)));
  }
}
