using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Domain.Events;

// POSITION AND GRADE DOMAIN EVENTS (FP-008 domain-model, ADR-009).
//
// ---- WHAT THEY CARRY, AND WHAT THEY DELIBERATELY DO NOT.
//
// Identifiers, status, rank and the occurrence time — and nothing else. No code, no title, no name, no
// actor, no free-form reason text. An event is the most widely-fanned-out thing an aggregate produces, so
// anything descriptive placed here spreads to every consumer, log and trace that touches it. This matches
// `EmployeeEvents` and `DepartmentEvents` exactly.
//
// ---- NO MONETARY AMOUNT APPEARS IN ANY EVENT, AND THAT IS DELIBERATE.
//
// `SalaryGradeCreated` and `SalaryGradeUpdated` carry `IsPriced` — whether a band exists — and never the
// amounts themselves. Pay bands are the one thing in FP-008 sensitive enough to warrant a permission of
// their own (`DEC-POS-0018` separates `HR.SalaryGrades.View` for exactly this reason), and an event that
// carried them would hand the figures to every subscriber and every trace regardless of who may read them.
// A permission that guards a table while the event stream publishes its contents guards nothing.
//
// ---- NOTHING CONSUMES THEM IN PHASE 1, AND THEY ARE RAISED ANYWAY.
//
// They are the seam any future org-chart or grade-structure projection would subscribe to. Raising them now
// means the aggregates' vocabulary is settled before handlers are written against it, rather than being
// retrofitted once callers exist.
//
// ---- `EmployeePositionChanged` IS NOT HERE, AND ITS ABSENCE IS SEQUENCING.
//
// It belongs to `Employee`, which does not reference positions until Phase 3. FP-007 placed the equivalent
// department event in `EmployeeEvents` when Phase 3 gave it a caller, and this follows that ordering rather
// than declaring a record nothing can raise.

public sealed record PositionCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid PositionId,
  Guid TenantId,
  Guid CompanyId,
  Guid? JobGradeId,
  PositionStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

// Carries both grades so a consumer can tell a re-grade from a first grading without re-reading the row.
public sealed record PositionUpdated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid PositionId,
  Guid TenantId,
  Guid CompanyId,
  Guid? PreviousJobGradeId,
  Guid? NewJobGradeId) : DomainEvent(EventId, OccurredUtc);

public sealed record PositionDeactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid PositionId,
  Guid TenantId,
  Guid CompanyId,
  PositionStatus PreviousStatus,
  PositionStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record PositionReactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid PositionId,
  Guid TenantId,
  Guid CompanyId,
  PositionStatus PreviousStatus,
  PositionStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record JobGradeCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid JobGradeId,
  Guid TenantId,
  Guid CompanyId,
  int RankOrder,
  Guid? SalaryGradeId,
  JobGradeStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record JobGradeUpdated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid JobGradeId,
  Guid TenantId,
  Guid CompanyId,
  int RankOrder,
  Guid? PreviousSalaryGradeId,
  Guid? NewSalaryGradeId) : DomainEvent(EventId, OccurredUtc);

public sealed record JobGradeDeactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid JobGradeId,
  Guid TenantId,
  Guid CompanyId,
  JobGradeStatus PreviousStatus,
  JobGradeStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record JobGradeReactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid JobGradeId,
  Guid TenantId,
  Guid CompanyId,
  JobGradeStatus PreviousStatus,
  JobGradeStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

// `IsPriced`, never the amounts. See the note above.
public sealed record SalaryGradeCreated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid SalaryGradeId,
  Guid TenantId,
  Guid CompanyId,
  int RankOrder,
  bool IsPriced,
  SalaryGradeStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record SalaryGradeUpdated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid SalaryGradeId,
  Guid TenantId,
  Guid CompanyId,
  int RankOrder,
  bool IsPriced) : DomainEvent(EventId, OccurredUtc);

public sealed record SalaryGradeDeactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid SalaryGradeId,
  Guid TenantId,
  Guid CompanyId,
  SalaryGradeStatus PreviousStatus,
  SalaryGradeStatus NewStatus) : DomainEvent(EventId, OccurredUtc);

public sealed record SalaryGradeReactivated(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid SalaryGradeId,
  Guid TenantId,
  Guid CompanyId,
  SalaryGradeStatus PreviousStatus,
  SalaryGradeStatus NewStatus) : DomainEvent(EventId, OccurredUtc);
