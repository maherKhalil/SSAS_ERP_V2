using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record CompanyProfileUpdated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid CompanyId,
  Guid TenantId) : DomainEvent(EventId, OccurredUtc);
