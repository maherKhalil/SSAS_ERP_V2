using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationCredentialFailureRecorded(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  long AuthenticationAccountId,
  int FailedAttemptCount) : DomainEvent(EventId, OccurredUtc);
