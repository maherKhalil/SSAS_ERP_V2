using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid TenantId,
  TenantStatus NewStatus,
  TenantStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);
