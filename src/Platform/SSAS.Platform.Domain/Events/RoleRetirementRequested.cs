using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record RoleRetirementRequested(Guid EventId, DateTimeOffset OccurredUtc, Guid TenantId, long RoleId)
  : DomainEvent(EventId, OccurredUtc);
