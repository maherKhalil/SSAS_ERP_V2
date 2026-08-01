using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationAccountSetupCompleted(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  long IdentityId,
  long SecurityVersion) : DomainEvent(EventId, OccurredUtc);
