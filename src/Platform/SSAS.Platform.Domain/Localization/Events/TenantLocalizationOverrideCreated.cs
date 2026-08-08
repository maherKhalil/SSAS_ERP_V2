using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Domain.Localization.Events;

public sealed record TenantLocalizationOverrideCreated : DomainEvent
{
  public TenantLocalizationOverrideCreated(
    Guid eventId,
    DateTimeOffset occurredUtc,
    Guid tenantId,
    Guid overrideId,
    string resourceKey,
    string culture,
    long currentVersionNumber,
    long tenantLocalizationVersion,
    long catalogVersion)
    : base(eventId, occurredUtc)
  {
    TenantId = tenantId;
    OverrideId = overrideId;
    ResourceKey = resourceKey;
    Culture = culture;
    CurrentVersionNumber = currentVersionNumber;
    TenantLocalizationVersion = tenantLocalizationVersion;
    CatalogVersion = catalogVersion;
  }

  public Guid TenantId { get; }
  public Guid OverrideId { get; }
  public string ResourceKey { get; }
  public string Culture { get; }
  public long CurrentVersionNumber { get; }
  public long TenantLocalizationVersion { get; }
  public long CatalogVersion { get; }
}
