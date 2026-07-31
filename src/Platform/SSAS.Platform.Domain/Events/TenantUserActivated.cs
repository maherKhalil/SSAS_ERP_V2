using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantUserActivated(Guid EventId, DateTimeOffset OccurredUtc, Guid TenantId, long IdentityId)
  : DomainEvent(EventId, OccurredUtc);
