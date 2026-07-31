using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record RolePermissionRemoved(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid TenantId,
  long RoleId,
  string Permission) : DomainEvent(EventId, OccurredUtc);
