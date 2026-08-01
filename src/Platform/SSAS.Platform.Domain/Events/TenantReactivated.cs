using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantReactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid TenantId,
  TenantStatus PreviousStatus,
  TenantStatus NewStatus,
  TenantStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);
