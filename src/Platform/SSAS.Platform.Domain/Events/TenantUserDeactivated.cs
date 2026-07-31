using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantUserDeactivated(Guid EventId, DateTimeOffset OccurredUtc, Guid TenantId, long TenantUserId)
  : DomainEvent(EventId, OccurredUtc);
