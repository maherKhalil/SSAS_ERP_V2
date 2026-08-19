using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Domain.Events;

// EMPLOYEE DOMAIN EVENTS (FP-006 domain-model, NFR-EMP-0307).
//
// ---- WHAT THEY CARRY, AND WHAT THEY DELIBERATELY DO NOT.
//
// Identifiers, status, branch identifiers, the occurrence time and a BOUNDED reason code — and nothing else.
// No employee name, no national identifier, no employee number, no free-form reason text. An event is the
// most widely-fanned-out thing the aggregate produces, so anything personal placed here spreads to every
// consumer, log and trace that touches it.
//
// Correlation, request, actor and trace metadata stay outside Domain and are attached by the existing
// dispatch infrastructure. Events are dispatched only after successful persistence, through the post-commit
// dispatcher; no outbox and no integration event is introduced.

public sealed record EmployeeCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  Guid BranchId,
  EmployeeStatus NewStatus,
  EmployeeStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);

public sealed record EmployeeProfileUpdated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId) : DomainEvent(EventId, OccurredUtc);

public sealed record EmployeeActivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  EmployeeStatus PreviousStatus,
  EmployeeStatus NewStatus,
  EmployeeStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);

public sealed record EmployeeDeactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  EmployeeStatus PreviousStatus,
  EmployeeStatus NewStatus,
  EmployeeStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);

public sealed record EmployeeTerminated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  EmployeeStatus PreviousStatus,
  EmployeeStatusChangeReason StatusChangeReason) : DomainEvent(EventId, OccurredUtc);

// The branch identifiers ARE carried, unlike anything personal: a consumer reacting to a relocation needs
// to know where from and where to, and neither identifier says anything about the person.
public sealed record EmployeeTransferred(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  Guid SourceBranchId,
  Guid DestinationBranchId,
  EmployeeBranchTransferReason TransferReason) : DomainEvent(EventId, OccurredUtc);
