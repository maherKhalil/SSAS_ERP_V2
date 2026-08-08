using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain.Localization;
using LocalizationDomainErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

public sealed class GetTenantLocalizationResourceQueryHandler(
  ILocalizationCatalog catalog,
  ITenantLocalizationAdministrationReadService administrationReadService,
  ILocalizationTextResolver resolver,
  IRequestTenantEligibility eligibility,
  ICurrentTenant currentTenant)
{
  public async Task<Result<LocalizationAdministrationDetail>> HandleAsync(
    GetTenantLocalizationResourceQuery query,
    CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId) return Result.Failure<LocalizationAdministrationDetail>(LocalizationDomainErrors.TenantIneligible);
    var culture = LocalizationCulture.Create(query.Culture);
    var key = ResourceKey.Create(query.ResourceKey);
    if (culture.IsFailure || key.IsFailure) return Result.Failure<LocalizationAdministrationDetail>(culture.IsFailure ? culture.Error : key.Error);
    var tenantEligibility = await eligibility.GetEligibilityAsync(tenantId, cancellationToken);
    if (!tenantEligibility.IsAuthenticationEligible) return Result.Failure<LocalizationAdministrationDetail>(LocalizationDomainErrors.TenantIneligible);
    if (!catalog.TryGet(key.Value, out var definition)) return Result.Failure<LocalizationAdministrationDetail>(LocalizationDomainErrors.ResourceNotFound);

    var overrides = await administrationReadService.ReadAsync(tenantId, culture.Value, [key.Value], cancellationToken);
    var resolved = await resolver.ResolveAsync(new LocalizationResolutionRequest(key.Value.Value, culture.Value.Value), cancellationToken);
    if (resolved.IsFailure) return Result.Failure<LocalizationAdministrationDetail>(resolved.Error);
    var resource = ListTenantLocalizationResourcesQueryHandler.Map(definition, culture.Value, overrides.SingleOrDefault(), resolved.Value, catalog.CatalogVersion.Value);
    return Result.Success(new LocalizationAdministrationDetail(resource, definition.EnglishDefault, definition.ArabicDefault));
  }
}
