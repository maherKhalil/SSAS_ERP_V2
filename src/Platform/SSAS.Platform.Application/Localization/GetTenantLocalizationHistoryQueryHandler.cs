using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain.Localization;
using LocalizationErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

public sealed class GetTenantLocalizationHistoryQueryHandler(
  ITenantLocalizationHistoryReadService historyReadService,
  ITenantAuthenticationEligibilityReadService eligibilityReadService,
  ICurrentTenant currentTenant)
{
  public async Task<Result<LocalizationHistoryResult>> HandleAsync(
    GetTenantLocalizationHistoryQuery query,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<LocalizationHistoryResult>(LocalizationErrors.TenantIneligible);
    }

    var eligibility = await eligibilityReadService.GetEligibilityAsync(tenantId, cancellationToken);
    if (!eligibility.IsAuthenticationEligible)
    {
      return Result.Failure<LocalizationHistoryResult>(LocalizationErrors.TenantIneligible);
    }

    var key = ResourceKey.Create(query.ResourceKey);
    var culture = LocalizationCulture.Create(query.Culture);
    if (key.IsFailure || culture.IsFailure)
    {
      return Result.Failure<LocalizationHistoryResult>(key.IsFailure ? key.Error : culture.Error);
    }

    var history = await historyReadService.GetAsync(tenantId, key.Value, culture.Value, cancellationToken);
    return history is null
      ? Result.Failure<LocalizationHistoryResult>(LocalizationErrors.OverrideMissing)
      : Result.Success(history);
  }
}
