using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ITenantLocalizationOverrideRepository
{
  Task<TenantLocalizationOverride?> GetForUpdateAsync(
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
    CancellationToken cancellationToken = default);

  Task<LocalizationVersionSnapshot?> GetVersionSnapshotAsync(
    Guid overrideId,
    TenantOverrideVersion versionNumber,
    CancellationToken cancellationToken = default);

  Task AddAsync(TenantLocalizationOverride localizationOverride, CancellationToken cancellationToken = default);
}
