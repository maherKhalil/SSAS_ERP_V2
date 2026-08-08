using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Localization;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface ITenantLocalizationHistoryReadService
{
  Task<LocalizationHistoryResult?> GetAsync(
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
    int pageNumber = 1,
    int pageSize = 50,
    CancellationToken cancellationToken = default);
}
