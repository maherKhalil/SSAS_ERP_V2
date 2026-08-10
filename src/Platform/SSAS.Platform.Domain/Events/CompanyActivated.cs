using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Events;

public sealed record CompanyActivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid CompanyId,
  Guid TenantId,
  CompanyStatus PreviousStatus,
  CompanyStatus NewStatus,
  CompanyStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);
