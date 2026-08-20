using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Domain.Events;

// DEPARTMENT DOMAIN EVENTS (ADR-026, FP-007 domain-model).
//
// ---- WHAT THEY CARRY, AND WHAT THEY DELIBERATELY DO NOT.
//
// Identifiers, status and the occurrence time — and nothing else. No department code, no name, no actor, no
// free-form reason text. An event is the most widely-fanned-out thing an aggregate produces, so anything
// descriptive placed here spreads to every consumer, log and trace that touches it. This matches
// `EmployeeEvents` exactly.
//
// ---- NOTHING CONSUMES THEM IN PHASE 1, AND THEY ARE RAISED ANYWAY.
//
// They are the seam a later phase writes `EmployeeDepartmentAssignment` history through, and the seam any
// future org-chart projection would subscribe to. Raising them now means the aggregate's vocabulary is
// settled before handlers are written against it, rather than being retrofitted once callers exist.

public sealed record DepartmentCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DepartmentId,
  Guid TenantId,
  Guid CompanyId,
  Guid? ParentDepartmentId,
  DepartmentStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record DepartmentDescriptionUpdated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DepartmentId,
  Guid TenantId,
  Guid CompanyId) : DomainEvent(EventId, OccurredUtc);

// Carries both parents so a consumer can tell a move from a promotion to root without re-reading the row.
public sealed record DepartmentParentChanged(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DepartmentId,
  Guid TenantId,
  Guid CompanyId,
  Guid? PreviousParentDepartmentId,
  Guid? NewParentDepartmentId) : DomainEvent(EventId, OccurredUtc);

public sealed record DepartmentDeactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DepartmentId,
  Guid TenantId,
  Guid CompanyId,
  DepartmentStatus PreviousStatus,
  DepartmentStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record DepartmentReactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid DepartmentId,
  Guid TenantId,
  Guid CompanyId,
  DepartmentStatus PreviousStatus,
  DepartmentStatus NewStatus) : DomainEvent(EventId, OccurredUtc);
