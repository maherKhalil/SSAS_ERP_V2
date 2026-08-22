using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Events;

namespace SSAS.HR.Domain.Positions;

// THE AGGREGATE THAT CLOSES BR-HR-0006 (REQ-HR-0200, FP-008 domain-model, DEC-POS-0001).
//
// ================================================================================================
// IT IS DELIBERATELY **NOT** IBranchOwnedEntity. THIS IS THE CLASSIFICATION, NOT AN OMISSION.
// ================================================================================================
//
// `BR-PLT-0013` scopes branch ownership to TRANSACTIONS, and a position is a master organizational record.
// The decisive argument is `ADR-024`, exactly as it was for Department: employee branch transfer is a
// sanctioned branch-only operation, so a branch-owned position would strand the employee's position on every
// transfer and break `BR-HR-0006` — and `ADR-024` provides for nothing of the kind.
//
// `ADR-026`'s deferred obligations required this to be "decided explicitly, not by copying", which is why
// `DEC-POS-0001` states the reasoning independently rather than citing Department. An architecture guard
// asserts the absence of `IBranchOwnedEntity` and of any `BranchId` in the composed model.
//
// ================================================================================================
// THERE IS NO REFERENCE TO ANY EMPLOYEE, AND THAT ABSENCE IS LOAD-BEARING (DEC-POS-0002).
// ================================================================================================
//
// No incumbent column, no holder collection, no association table pointing this way. Who holds a position is
// answered by querying Employees by `PositionId`.
//
// `Employee.PositionId -> Position` plus ANY `Position.* -> Employee` key is a cycle in the table-level
// foreign-key graph. `TenantCutoverCopyPlan.Order` places tables principals-before-dependents and returns
// `CutoverCopyOrderUndecidable` when no table is ready — verified in source for Department's naive manager
// column (`RISK-DEP-001`). Shared→Dedicated cutover would stop working for EVERY tenant, and it would not
// degrade or warn. One convenience column is all it takes; `TS-POS-0044` asserts the failure executably.
//
// ================================================================================================
// THERE IS NO DepartmentId EITHER (OD-POS-003).
// ================================================================================================
//
// Jobs are defined centrally, as a company-wide catalog. `Employee.DepartmentId` remains the SINGLE
// authority on an employee's department, so no invariant has to keep two copies of that fact in step. The
// accepted cost is that the org chart cannot list a department's jobs except through the employees holding
// both; if that view is ever wanted it is a read model, never a column here.
//
// ---- WHAT PHASE 1 DELIBERATELY DOES NOT ENFORCE.
//
// `JobGradeId` is accepted as an identifier and checked only for the empty-Guid case. That the grade exists,
// belongs to the same company, and is `Active` (`BRULE-POS-0009`) are repository lookups the aggregate
// cannot perform, and they are Phase 2. **Nothing here pretends to enforce them**, because a half-enforced
// invariant that looks complete is worse than one that visibly is not.
public sealed class Position
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private string normalizedCode = string.Empty;

  // The search form of the label, maintained beside the code's (`DEC-POS-0030`). Set in exactly the two
  // places `normalizedCode` is set, so the two can never disagree about which write produced them.
  private string normalizedTitle = string.Empty;

  private Position(
    Guid positionId,
    PositionCode code,
    PositionTitle title,
    Guid? jobGradeId,
    string actor,
    DateTimeOffset occurredUtc) : base(positionId)
  {
    Code = code;
    normalizedCode = code.NormalizedValue;
    normalizedTitle = title.NormalizedValue;
    Title = title;
    JobGradeId = jobGradeId;
    Status = PositionStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
  }

  private Position()
    : base(Guid.Empty)
  {
    Code = null!;
    Title = null!;
    StatusChangedBy = string.Empty;
  }

  public Guid PositionId => Id;

  // ---- THE TWO OWNERSHIP DIMENSIONS. There is deliberately no third.
  //
  // Both are stamped by the shared persistence boundaries from trusted server context, never by a caller.
  // The interface setters exist for that stamping and for nothing else.
  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public PositionCode Code { get; private set; }

  public string NormalizedCode => normalizedCode;

  public string NormalizedTitle => normalizedTitle;

  public PositionTitle Title { get; private set; }

  // NULL MEANS UNGRADED. A position may exist before it is placed on the ladder, which is the same
  // define-before-classify case that makes `SalaryBand` optional.
  public Guid? JobGradeId { get; private set; }

  public PositionStatus Status { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; }

  // CreatedUtc/CreatedBy/ModifiedUtc/ModifiedBy are owned by the IAuditableEntity persistence
  // infrastructure and are never stamped by the Domain.
  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<Position> Create(
    PositionCode code,
    PositionTitle title,
    Guid? jobGradeId,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure<Position>(PositionErrors.InvalidCode);
    }

    if (title is null)
    {
      return Result.Failure<Position>(PositionErrors.InvalidTitle);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Position>(PositionErrors.InvalidActor);
    }

    // An empty identifier is not a grade. Distinct from "no grade", which is `null` and is legal.
    if (jobGradeId == Guid.Empty)
    {
      return Result.Failure<Position>(PositionErrors.InvalidGradeReference);
    }

    return Result.Success(new Position(
      Guid.NewGuid(), code, title, jobGradeId, actor.Trim(), occurredUtc));
  }

  // Called by the application once the trusted tenant and company are known — which is after the aggregate
  // exists, because both come from the execution context rather than from the caller's input.
  public Result StampCreated(Guid tenantId, Guid companyId, Guid eventId, DateTimeOffset occurredUtc)
  {
    RaiseDomainEvent(new PositionCreated(
      eventId, occurredUtc, Id, tenantId, companyId, JobGradeId, PositionStatus.Active));

    return Result.Success();
  }

  // ---- RETITLE, RECODE AND RE-GRADE.
  //
  // Ownership and status are absent from this operation by construction, not by validation — status has its
  // own. Re-grading lives here rather than in a route of its own: `DEC-POS-0018` records the grouping as
  // deliberate, since a role able to retitle a position but not re-grade it is a distinction no requirement
  // asks for.
  public Result UpdateDescription(
    PositionCode code,
    PositionTitle title,
    Guid? jobGradeId,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure(PositionErrors.InvalidCode);
    }

    if (title is null)
    {
      return Result.Failure(PositionErrors.InvalidTitle);
    }

    if (jobGradeId == Guid.Empty)
    {
      return Result.Failure(PositionErrors.InvalidGradeReference);
    }

    var previousJobGradeId = JobGradeId;

    Code = code;
    normalizedCode = code.NormalizedValue;
    normalizedTitle = title.NormalizedValue;
    Title = title;
    JobGradeId = jobGradeId;

    RaiseDomainEvent(new PositionUpdated(
      eventId, occurredUtc, Id, TenantId, CompanyId, previousJobGradeId, jobGradeId));

    return Result.Success();
  }

  // ---- LIFECYCLE. Active <-> Inactive, reversibly.
  //
  // NEITHER METHOD CONSULTS INCUMBENTS, and under `OD-POS-005` neither should. "One ACTIVE position"
  // qualifies the ASSIGNMENT, not the position's lifecycle status, so deactivating a position that people
  // hold is allowed and they keep it — `BRULE-POS-0014`, mirroring `BRULE-DEP-0015`. What an inactive
  // position refuses is a NEW assignment, and that refusal belongs to the operation doing the assigning.
  //
  // Had the other reading been ruled, this method would have needed a repository lookup and a refusal; the
  // ruling is what makes it a pure state transition.
  public Result Deactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != PositionStatus.Active)
    {
      return Result.Failure(PositionErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    Status = PositionStatus.Inactive;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new PositionDeactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId, PositionStatus.Active, PositionStatus.Inactive));

    return Result.Success();
  }

  public Result Reactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != PositionStatus.Inactive)
    {
      return Result.Failure(PositionErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    Status = PositionStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new PositionReactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId, PositionStatus.Inactive, PositionStatus.Active));

    return Result.Success();
  }

  private static bool IsValidActor(string actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Trim().Length <= ActorMaximumLength;

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
