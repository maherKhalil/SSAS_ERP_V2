using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record RolePermissionAssigned(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid TenantId,
  long RoleId,
  string Permission) : DomainEvent(EventId, OccurredUtc);
