using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Events;

public sealed record CompanyCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid CompanyId,
  Guid TenantId,
  CompanyStatus NewStatus,
  CompanyStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);
