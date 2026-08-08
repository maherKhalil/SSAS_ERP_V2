using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Application.Abstractions.Localization;

public interface ITenantLocalizationAdministrationReadService
{
  Task<IReadOnlyList<TenantLocalizationOverrideAdministrationReadModel>> ReadAsync(
    Guid tenantId,
    LocalizationCulture culture,
    IReadOnlyCollection<ResourceKey> resourceKeys,
    CancellationToken cancellationToken = default);
}

public sealed record TenantLocalizationOverrideAdministrationReadModel(
  string ResourceKey,
  string? Value,
  bool IsActive,
  long TenantOverrideVersion,
  long CurrentVersionNumber,
  long CatalogVersion,
  int ResourceVersion,
  byte[] PlaceholderFingerprint,
  byte[] CompatibilityFingerprint,
  byte[] RowVersion,
  DateTimeOffset ModifiedUtc,
  long? EligibleUndoTargetVersion);
