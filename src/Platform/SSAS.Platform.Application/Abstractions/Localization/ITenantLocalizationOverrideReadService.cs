using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Application.Abstractions.Localization;

public interface ITenantLocalizationOverrideReadService
{
  Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> ReadAsync(
    Guid tenantId,
    LocalizationCulture culture,
    IReadOnlyCollection<ResourceKey> resourceKeys,
    CancellationToken cancellationToken = default);
}
