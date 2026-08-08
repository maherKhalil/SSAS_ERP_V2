using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Localization.Events;

public sealed record TenantLocalizationOverrideRestoredDefault(
  Guid EventIdentifier,
  DateTimeOffset EventOccurredUtc,
  Guid TenantId,
  Guid OverrideId,
  string ResourceKey,
  string Culture,
  long PriorVersionNumber,
  long CurrentVersionNumber,
  long TenantLocalizationVersion,
  long CatalogVersion) : DomainEvent(EventIdentifier, EventOccurredUtc);
