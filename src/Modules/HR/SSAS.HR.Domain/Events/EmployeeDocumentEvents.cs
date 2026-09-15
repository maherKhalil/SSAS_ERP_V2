using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.Domain.Events;

public sealed record EmployeeDocumentUploaded(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DocumentId,
  Guid TenantId,
  Guid CompanyId,
  Guid EmployeeId,
  EmployeeDocumentType DocumentType,
  long ByteCount) : DomainEvent(EventId, OccurredUtc);

public sealed record EmployeeDocumentWithdrawn(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DocumentId,
  Guid TenantId,
  Guid CompanyId,
  Guid EmployeeId,
  string WithdrawnBy) : DomainEvent(EventId, OccurredUtc);
