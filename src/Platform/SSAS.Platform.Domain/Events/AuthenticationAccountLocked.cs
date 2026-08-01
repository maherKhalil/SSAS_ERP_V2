using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationAccountLocked(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  long AuthenticationAccountId,
  DateTimeOffset LockoutEndUtc) : DomainEvent(EventId, OccurredUtc);
