using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record RoleCreated(Guid EventId, DateTimeOffset OccurredUtc, Guid TenantId, string RoleName)
  : DomainEvent(EventId, OccurredUtc);
