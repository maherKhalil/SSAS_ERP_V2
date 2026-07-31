using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantUserRoleRemoved(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid TenantId,
  long TenantUserId,
  long RoleId) : DomainEvent(EventId, OccurredUtc);
