using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationAccountDisabled(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  long AuthenticationAccountId,
  long SecurityVersion) : DomainEvent(EventId, OccurredUtc);
