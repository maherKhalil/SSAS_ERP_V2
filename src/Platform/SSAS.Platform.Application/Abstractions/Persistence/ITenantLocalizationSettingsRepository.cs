using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ITenantLocalizationSettingsRepository
{
  Task<TenantLocalizationSettings?> GetForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default);

  Task<TenantLocalizationSettings> GetOrCreateForUpdateAsync(
    Guid tenantId,
    LocalizationCulture defaultCulture,
    CancellationToken cancellationToken = default);
}
