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

// ---- FP-007 PHASE 3 (REQ-HR-0102).
//
// The department identifiers are carried for the same reason the branch ones are: a consumer reacting to a
// reorganization needs to know where from and where to, and neither identifier says anything about the
// person.
//
// THE REASON TEXT IS NOT CARRIED, and that is deliberate. It is free-form operator input persisted for the
// audit record alone; putting it on an event would push unbounded text into every downstream consumer and
// into whatever they log. The reason CODE is omitted with it, because unlike a branch transfer's enum this
// one is nullable free text under the approved Phase 1 model and would carry the same problem.
public sealed record EmployeeDepartmentChanged(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  Guid SourceDepartmentId,
  Guid DestinationDepartmentId) : DomainEvent(EventId, OccurredUtc);

// ---- THE POSITION CHANGE (FP-008 Phase 3, FR-POS-0211).
//
// It lives HERE, on the employee's event list, and not in `PositionEvents.cs` — because the thing that
// changed is an EMPLOYEE. FP-008 Phase 1 deliberately left it out of the position event set for exactly this
// reason and said so, rather than defining it early beside the aggregate it names.
//
// The position identifiers are carried for the same reason the branch and department ones are: a consumer
// reacting to a promotion needs to know where from and where to, and neither identifier says anything about
// the person.
//
// THE REASON TEXT AND CODE ARE NOT CARRIED, on the identical terms as `EmployeeDepartmentChanged`: both are
// free-form operator input persisted for the audit record alone, and putting them on an event would push
// unbounded text into every downstream consumer. `SourcePositionId` is non-nullable here even though the
// STORED record's is nullable — an initial assignment is part of `EmployeeCreated`, not of a change.
public sealed record EmployeePositionChanged(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid EmployeeId,
  Guid TenantId,
  Guid CompanyId,
  Guid SourcePositionId,
  Guid DestinationPositionId) : DomainEvent(EventId, OccurredUtc);
