using SSAS.HR.Contracts.Employment;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Events;

namespace SSAS.HR.Domain.Employees;

// THE FIRST BUSINESS RECORD OWNED ALONG ALL THREE DIMENSIONS (FP-006, ADR-014 r1.1, ADR-023, ADR-024,
// ADR-025).
//
// Tenant ownership answers *whose data is this*; company ownership answers *which legal entity employs
// them*; branch ownership answers *which operating location they work at*. Company and Branch are SIBLING
// dimensions beneath the tenant, not nested, so each is an independent classification and neither can
// substitute for the other.
//
// Employee is the first production consumer of `IBranchOwnedEntity` and of `ICompanyOwnedEntity`, which is
// why this type is what finally converts ADR-023 decisions 10, 16 and 18 from structurally implemented to
// runtime-proven (the ADR-023 LOW-1 obligation).
//
// ---- WHAT THE AGGREGATE DOES NOT LET YOU DO.
//
// There is no setter for TenantId beyond the ownership interface the persistence layer stamps through, no
// way to change CompanyId or EmployeeNumber after creation, and no ordinary path that changes BranchId.
// Those are enforced twice: unavailable here by construction, and refused by the shared write boundaries as
// defence in depth. Getting past one of them is a bug; getting past both is not reachable.
//
// There is no PositionId or ManagerId. BR-HR-0006 and BR-HR-0007 are retained as binding rules whose
// enforcement is deferred until those aggregates exist (DEC-EMP-0018, DEC-EMP-0031) — deferred, not
// discarded, and no placeholder column stands in for them.
//
// ---- DEPARTMENT IS NOW REAL (FP-007 Phase 3, BR-HR-0005).
//
// DepartmentId is a FOURTH classification and NOT a fourth ownership dimension. Ownership is tenant,
// company and branch; a department is org structure within a company. The distinction is load-bearing:
// a department spans the branches of its company, so it can neither narrow nor widen branch scope, and
// nothing about a department is consulted when deciding who may see this record.
//
// The consequence is that the two move INDEPENDENTLY. A branch transfer leaves DepartmentId untouched; a
// department change leaves BranchId untouched. Neither operation is expressible as the other.
public sealed class Employee
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IBranchOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private readonly List<EmployeeBranchAssignment> branchAssignments = [];

  private readonly List<Departments.EmployeeDepartmentAssignment> departmentAssignments = [];

  // The third history, appended by exactly the same two operations as the other two: `StampInitialAssignment`
  // at creation and `ChangePosition` thereafter (`DEC-POS-0008`, `BRULE-POS-0018`).
  private readonly List<Positions.EmployeePositionAssignment> positionAssignments = [];

  private string normalizedEmployeeNumber = string.Empty;

  private string? normalizedNationalId;

  private Employee(
    Guid employeeId,
    EmployeeNumber employeeNumber,
    EmployeeFullName fullName,
    NationalId? nationalId,
    DateTimeOffset employmentDate,
    string actor,
    DateTimeOffset occurredUtc,
    EmploymentType employmentType) : base(employeeId)
  {
    EmployeeNumber = employeeNumber;
    normalizedEmployeeNumber = employeeNumber.NormalizedValue;
    FullName = fullName;
    NationalId = nationalId;
    normalizedNationalId = nationalId?.NormalizedValue;
    EmploymentDate = employmentDate.ToUniversalTime();
    EmploymentType = employmentType;
    Status = EmployeeStatus.Active;
    StatusChangeReasonCode = EmployeeStatusChangeReason.Created;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
  }

  private Employee()
    : base(Guid.Empty)
  {
    EmployeeNumber = null!;
    FullName = null!;
    StatusChangedBy = string.Empty;
  }

  public Guid EmployeeId => Id;

  // ---- THE THREE OWNERSHIP DIMENSIONS.
  //
  // All three are stamped by the shared persistence boundaries from trusted server context, never by a
  // caller. The interface setters exist for that stamping and for nothing else.
  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  // The CURRENT operating branch, authoritative (ADR-024 decision 1). It changes only through the sanctioned
  // transfer channel; the history in BranchAssignments records how it got here.
  public Guid BranchId { get; set; }

  // ---- THE CURRENT DEPARTMENT, AND WHY ITS SETTER IS PRIVATE WHILE BranchId'S IS NOT.
  //
  // BranchId is public-set because `IBranchOwnedEntity` requires it: the branch write boundary stamps it
  // from trusted server context, and the interface is how it reaches in. DepartmentId has no such boundary
  // and no such interface — it is supplied by the caller and validated by the application — so there is no
  // reason for anything outside this aggregate to assign it, and it is private-set accordingly.
  //
  // It therefore changes through exactly two paths, both of which append history in the same unit of work:
  // StampInitialAssignment at creation, and ChangeDepartment thereafter. There is no third.
  public Guid DepartmentId { get; private set; }

  // ---- THE THIRD OWNERSHIP-ADJACENT FIELD, AND IT HAS NO PUBLIC SETTER (ADR-026 d.6, DEC-POS-0010).
  //
  // `BranchId` has one only because `IBranchOwnedEntity` needs it for stamping; `DepartmentId` has none;
  // this has none. Two operations write it — `StampInitialAssignment` at creation and `ChangePosition`
  // thereafter — and there is no third, which is what makes "the position changed" and "a history row was
  // appended" the same event rather than two that could drift.
  //
  // It is REQUIRED for every employee (`OD-POS-001`, `BR-HR-0006`). There is no unbound cohort and no
  // transitional nullable phase: the migration asserted the table was empty before the column existed.
  public Guid PositionId { get; private set; }

  public EmployeeNumber EmployeeNumber { get; private set; }

  public string NormalizedEmployeeNumber => normalizedEmployeeNumber;

  public NationalId? NationalId { get; private set; }

  public string? NormalizedNationalId => normalizedNationalId;

  public EmployeeFullName FullName { get; private set; }

  public DateTimeOffset EmploymentDate { get; private set; }

  // Null until termination, and required once terminated. A check constraint keeps the two from disagreeing.
  public DateTimeOffset? TerminationDate { get; private set; }

  // ---- HOW THIS EMPLOYEE IS ENGAGED (T-153). See `EmploymentType` for why this is not a pay field.
  //
  // `FullTime` is `default`, so every employee written before T-153 keeps the arrangement they already
  // had. **Payroll reads this through a purpose-named contract call, never through the roster
  // projection** — `DEC-PAY-0017` pins that projection's field list, and widening it would let every
  // future payroll feature read the value with no call site changing for anyone to review.
  public EmploymentType EmploymentType { get; private set; }

  public EmployeeStatus Status { get; private set; }

  public EmployeeStatusChangeReason StatusChangeReasonCode { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; }

  // CreatedUtc/CreatedBy/ModifiedUtc/ModifiedBy are owned by the IAuditableEntity persistence
  // infrastructure and are never stamped by the Domain.
  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  // THE SERIALIZATION POINT FOR TRANSFER (ADR-024 decision 7). Optimistic concurrency on the Employee is
  // what guarantees the append-only assignment log cannot fork.
  public byte[] RowVersion { get; private set; } = [];

  // Exposed read-only: history is appended by the aggregate's own operations, never by a caller reaching in.
  public IReadOnlyCollection<EmployeeBranchAssignment> BranchAssignments => branchAssignments;

  // The same guarantee for the department log. Append-only, and appended only from inside this type.
  public IReadOnlyCollection<Departments.EmployeeDepartmentAssignment> DepartmentAssignments =>
    departmentAssignments;

  public IReadOnlyCollection<Positions.EmployeePositionAssignment> PositionAssignments =>
    positionAssignments;

  // ---- CREATE.
  //
  // The initial branch assignment is produced HERE rather than by the caller, so an Employee with no branch
  // history cannot be constructed. BranchId is left empty deliberately: the branch write boundary stamps it
  // from the trusted execution context, and a caller-supplied value would be confirmed, never trusted.
  public static Result<Employee> Create(
    EmployeeNumber employeeNumber,
    EmployeeFullName fullName,
    NationalId? nationalId,
    DateTimeOffset employmentDate,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    EmploymentType employmentType = EmploymentType.FullTime)
  {
    if (employeeNumber is null)
    {
      return Result.Failure<Employee>(EmployeeErrors.InvalidEmployeeNumber);
    }

    if (fullName is null)
    {
      return Result.Failure<Employee>(EmployeeErrors.InvalidFullName);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Employee>(EmployeeErrors.InvalidActor);
    }

    if (employmentDate == default)
    {
      return Result.Failure<Employee>(EmployeeErrors.InvalidEmploymentDate);
    }

    return Result.Success(new Employee(
      Guid.NewGuid(), employeeNumber, fullName, nationalId, employmentDate, actor.Trim(), occurredUtc,
      employmentType));
  }

  // Called by the application once the trusted tenant, company and branch are known — which is after the
  // aggregate exists, because those come from the execution context rather than from the caller's input.
  //
  // Producing the initial assignment here keeps it in the SAME unit of work as the Employee: both commit or
  // neither does, so an Employee can never be persisted without its history (AC-EMP-0005).
  //
  // ---- FP-007 PHASE 3: THE DEPARTMENT ARRIVES THROUGH THE SAME DOOR.
  //
  // The initial DEPARTMENT assignment is produced here too, for the identical reason and with the identical
  // guarantee. The department is validated by the application before this is called — it must exist, be in
  // this tenant and company, and be Active — and what this method guarantees is narrower and different:
  // that the column and its first history row are written together or not at all.
  //
  // Unlike the branch, DepartmentId is ASSIGNED here rather than stamped by a boundary. Nothing else
  // stamps it, so if this method did not set it an Employee would commit with an empty department and a
  // history row claiming otherwise.
  //
  // FP-008 Phase 3 adds the POSITION on identical terms. The method now builds THREE records before it
  // mutates anything, which is why the construct-then-mutate ordering was worth keeping: a third record
  // that failed validation after two had been appended would leave an employee whose history claimed more
  // than its columns.
  public Result StampInitialAssignment(
    Guid tenantId,
    Guid companyId,
    Guid branchId,
    Guid departmentId,
    Guid positionId,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (branchAssignments.Count > 0)
    {
      return Result.Failure(EmployeeErrors.BranchHistoryImmutable);
    }

    if (departmentAssignments.Count > 0)
    {
      return Result.Failure(EmployeeErrors.DepartmentHistoryImmutable);
    }

    if (positionAssignments.Count > 0)
    {
      return Result.Failure(EmployeeErrors.PositionHistoryImmutable);
    }

    if (departmentId == Guid.Empty)
    {
      return Result.Failure(EmployeeErrors.DepartmentRequired);
    }

    // `BR-HR-0006`, enforced by the shape rather than by a nullable column someone might later relax.
    if (positionId == Guid.Empty)
    {
      return Result.Failure(EmployeeErrors.PositionRequired);
    }

    var assignment = EmployeeBranchAssignment.CreateInitial(
      tenantId, companyId, Id, branchId, occurredUtc, actor);
    if (assignment.IsFailure)
    {
      return Result.Failure(assignment.Error);
    }

    var departmentAssignment = Departments.EmployeeDepartmentAssignment.CreateInitial(
      tenantId, companyId, Id, departmentId, occurredUtc, actor);
    if (departmentAssignment.IsFailure)
    {
      return Result.Failure(departmentAssignment.Error);
    }

    var positionAssignment = Positions.EmployeePositionAssignment.CreateInitial(
      tenantId, companyId, Id, positionId, occurredUtc, actor);
    if (positionAssignment.IsFailure)
    {
      return Result.Failure(positionAssignment.Error);
    }

    // Nothing above this line mutated the aggregate. All three records are built and validated FIRST, so a
    // failure in the third cannot leave the first two appended to a half-stamped employee.
    DepartmentId = departmentId;
    PositionId = positionId;
    branchAssignments.Add(assignment.Value);
    departmentAssignments.Add(departmentAssignment.Value);
    positionAssignments.Add(positionAssignment.Value);

    RaiseDomainEvent(new EmployeeCreated(
      eventId, occurredUtc, Id, tenantId, companyId, branchId,
      EmployeeStatus.Active, EmployeeStatusChangeReason.Created));

    return Result.Success();
  }

  // ---- UPDATE PROFILE.
  //
  // Only the mutable profile fields. TenantId, CompanyId, BranchId, EmployeeId, EmployeeNumber and Status
  // are absent from this operation by construction, not by validation.
  public Result UpdateProfile(
    EmployeeFullName fullName,
    NationalId? nationalId,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (fullName is null)
    {
      return Result.Failure(EmployeeErrors.InvalidFullName);
    }

    if (Status == EmployeeStatus.Terminated)
    {
      return Result.Failure(EmployeeErrors.InvalidTransition);
    }

    FullName = fullName;
    NationalId = nationalId;
    normalizedNationalId = nationalId?.NormalizedValue;

    RaiseDomainEvent(new EmployeeProfileUpdated(eventId, occurredUtc, Id, TenantId, CompanyId));

    return Result.Success();
  }

  // ---- LIFECYCLE. Active <-> Inactive, and either into terminal Terminated.
  public Result Deactivate(
    EmployeeStatusChangeReason reason, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != EmployeeStatus.Active)
    {
      return Result.Failure(EmployeeErrors.InvalidTransition);
    }

    var transition = ApplyTransition(EmployeeStatus.Inactive, reason, actor, occurredUtc);
    if (transition.IsFailure)
    {
      return transition;
    }

    RaiseDomainEvent(new EmployeeDeactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId,
      EmployeeStatus.Active, EmployeeStatus.Inactive, reason));

    return Result.Success();
  }

  // Re-enablement only: a created Employee is already Active, so there is no separate Reactivate concept
  // and no route for one (BRULE-EMP-0002).
  public Result Activate(
    EmployeeStatusChangeReason reason, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != EmployeeStatus.Inactive)
    {
      return Result.Failure(EmployeeErrors.InvalidTransition);
    }

    var transition = ApplyTransition(EmployeeStatus.Active, reason, actor, occurredUtc);
    if (transition.IsFailure)
    {
      return transition;
    }

    RaiseDomainEvent(new EmployeeActivated(
      eventId, occurredUtc, Id, TenantId, CompanyId,
      EmployeeStatus.Inactive, EmployeeStatus.Active, reason));

    return Result.Success();
  }

  // ---- TERMINATE. Terminal, and NOT deletion: the record, its identifiers and its whole branch history
  // are retained, so reporting over periods before termination stays correct (BR-PLT-0003, BR-HR-0004).
  public Result Terminate(
    DateTimeOffset terminationDate,
    EmployeeStatusChangeReason reason,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status == EmployeeStatus.Terminated)
    {
      return Result.Failure(EmployeeErrors.InvalidTransition);
    }

    // BR-HR-0003. Enforced here AND by a check constraint, because a date that violates it is wrong
    // whichever path wrote it.
    if (terminationDate.ToUniversalTime() < EmploymentDate)
    {
      return Result.Failure(EmployeeErrors.TerminationBeforeEmployment);
    }

    var previous = Status;
    var transition = ApplyTransition(EmployeeStatus.Terminated, reason, actor, occurredUtc);
    if (transition.IsFailure)
    {
      return transition;
    }

    TerminationDate = terminationDate.ToUniversalTime();

    RaiseDomainEvent(new EmployeeTerminated(
      eventId, occurredUtc, Id, TenantId, CompanyId, previous, reason));

    return Result.Success();
  }

  // ---- TRANSFER.
  //
  // A DEDICATED OPERATION, NOT A PROPERTY ASSIGNMENT (REQ-HR-0004, ADR-024 decisions 3 and 10). The
  // application layer authorizes the destination and opens the sanctioned channel before calling this; the
  // aggregate's job is the domain rules and the history append, both of which happen atomically with the
  // BranchId change because they are one unit of work.
  //
  // Transfer is NOT a lifecycle transition: it changes no status and appears nowhere in the transition
  // graph. It is permitted from Active and Inactive — an employee on leave may still be reassigned, notably
  // when their branch is closing — and refused once Terminated.
  public Result<EmployeeBranchAssignment> Transfer(
    Guid destinationBranchId,
    EmployeeBranchTransferReason reason,
    string? reasonText,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status == EmployeeStatus.Terminated)
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.TransferAfterTermination);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.InvalidActor);
    }

    if (destinationBranchId == Guid.Empty || destinationBranchId == BranchId)
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.TransferDestinationUnchanged);
    }

    var sourceBranchId = BranchId;

    var assignment = EmployeeBranchAssignment.CreateTransfer(
      TenantId, CompanyId, Id, sourceBranchId, destinationBranchId, occurredUtc, actor.Trim(),
      reason, reasonText);
    if (assignment.IsFailure)
    {
      return Result.Failure<EmployeeBranchAssignment>(assignment.Error);
    }

    // The current branch moves and the history records that it moved, together. Neither is observable
    // without the other, because both land in the same save.
    BranchId = destinationBranchId;
    branchAssignments.Add(assignment.Value);

    RaiseDomainEvent(new EmployeeTransferred(
      eventId, occurredUtc, Id, TenantId, CompanyId, sourceBranchId, destinationBranchId, reason));

    return Result.Success(assignment.Value);
  }

  // ---- CHANGE DEPARTMENT (REQ-HR-0102, ADR-026).
  //
  // The department counterpart of Transfer, and deliberately its twin: a dedicated operation, never a
  // property assignment, that moves the current value and appends the record of the move in one step.
  //
  // ---- WHAT IT POINTEDLY DOES NOT TOUCH.
  //
  // BranchId. An employee who moves from Finance to Operations works at the same location the next morning.
  // The two dimensions are independent, and the absence of any BranchId reference in this method is the
  // enforcement of that — there is no rule to check because there is no assignment to guard.
  //
  // ---- WHY IT IS PERMITTED WHILE INACTIVE AND REFUSED ONCE TERMINATED.
  //
  // Same rule as Transfer, for the same reason: an employee on leave is still employed and may still be
  // reorganized, while a terminated record is closed and its history must stop moving.
  //
  // The DESTINATION's existence, tenant, company and Active status are the APPLICATION's to prove — they
  // require reading another aggregate, which this type cannot do. What is checked here is what is knowable
  // from local state alone.
  public Result<Departments.EmployeeDepartmentAssignment> ChangeDepartment(
    Guid destinationDepartmentId,
    string? reasonCode,
    string? reasonText,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status == EmployeeStatus.Terminated)
    {
      return Result.Failure<Departments.EmployeeDepartmentAssignment>(EmployeeErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Departments.EmployeeDepartmentAssignment>(EmployeeErrors.InvalidActor);
    }

    if (destinationDepartmentId == Guid.Empty)
    {
      return Result.Failure<Departments.EmployeeDepartmentAssignment>(EmployeeErrors.DepartmentRequired);
    }

    // A move to where the employee already is. Refused rather than silently succeeding, which is the same
    // answer Transfer gives an unchanged branch — and it is what keeps a no-op from appending a history row
    // that would describe no movement at all.
    if (destinationDepartmentId == DepartmentId)
    {
      return Result.Failure<Departments.EmployeeDepartmentAssignment>(EmployeeErrors.DepartmentUnchanged);
    }

    var sourceDepartmentId = DepartmentId;

    var assignment = Departments.EmployeeDepartmentAssignment.CreateChange(
      TenantId, CompanyId, Id, sourceDepartmentId, destinationDepartmentId, occurredUtc, actor.Trim(),
      reasonCode, reasonText);
    if (assignment.IsFailure)
    {
      return Result.Failure<Departments.EmployeeDepartmentAssignment>(assignment.Error);
    }

    // The current department moves and the history records that it moved, together. Neither is observable
    // without the other, because both land in the same save.
    DepartmentId = destinationDepartmentId;
    departmentAssignments.Add(assignment.Value);

    RaiseDomainEvent(new Events.EmployeeDepartmentChanged(
      eventId, occurredUtc, Id, TenantId, CompanyId, sourceDepartmentId, destinationDepartmentId));

    return Result.Success(assignment.Value);
  }

  // ---- CHANGE THE EMPLOYEE'S POSITION (FR-POS-0211, DEC-POS-0010, BRULE-POS-0017).
  //
  // THE ONLY WAY `PositionId` MOVES after creation. It is not a field on the ordinary profile update and
  // never will be: a promotion is a structural event with a history row, not a profile edit.
  //
  // The refusal ORDER is the package's, stated in `domain-model.md`: a terminated employee, an invalid
  // actor, an empty destination, then a destination equal to the current position. Order matters because
  // each answer tells the caller something, and a terminated employee should be told that first rather than
  // being told their destination is unchanged.
  //
  // The DESTINATION's existence, tenant, company and Active status are the APPLICATION's to prove — they
  // require reading another aggregate, which this type cannot do (`BRULE-POS-0016`, `BRULE-POS-0013`). What
  // is checked here is what is knowable from local state alone.
  //
  // ---- IT TOUCHES NEITHER BRANCH NOR DEPARTMENT (BRULE-POS-0019).
  //
  // The three dimensions are independent. A promotion does not relocate someone and does not move them
  // between departments, and the absence of those assignments here is what makes that true structurally.
  public Result<Positions.EmployeePositionAssignment> ChangePosition(
    Guid destinationPositionId,
    string? reasonCode,
    string? reasonText,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status == EmployeeStatus.Terminated)
    {
      return Result.Failure<Positions.EmployeePositionAssignment>(EmployeeErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Positions.EmployeePositionAssignment>(EmployeeErrors.InvalidActor);
    }

    if (destinationPositionId == Guid.Empty)
    {
      return Result.Failure<Positions.EmployeePositionAssignment>(EmployeeErrors.PositionRequired);
    }

    // A move to the position the employee already holds. Refused rather than silently succeeding, which is
    // the same answer `Transfer` and `ChangeDepartment` give — and it is what keeps a no-op from appending
    // a history row that would describe no movement at all.
    if (destinationPositionId == PositionId)
    {
      return Result.Failure<Positions.EmployeePositionAssignment>(EmployeeErrors.PositionUnchanged);
    }

    var sourcePositionId = PositionId;

    var assignment = Positions.EmployeePositionAssignment.CreateChange(
      TenantId, CompanyId, Id, sourcePositionId, destinationPositionId, occurredUtc, actor.Trim(),
      reasonCode, reasonText);
    if (assignment.IsFailure)
    {
      return Result.Failure<Positions.EmployeePositionAssignment>(assignment.Error);
    }

    // The current position moves and the history records that it moved, together. Neither is observable
    // without the other, because both land in the same save (`BRULE-POS-0018`).
    PositionId = destinationPositionId;
    positionAssignments.Add(assignment.Value);

    RaiseDomainEvent(new Events.EmployeePositionChanged(
      eventId, occurredUtc, Id, TenantId, CompanyId, sourcePositionId, destinationPositionId));

    return Result.Success(assignment.Value);
  }

  private Result ApplyTransition(
    EmployeeStatus next, EmployeeStatusChangeReason reason, string actor, DateTimeOffset occurredUtc)
  {
    // `Created` records a creation and nothing else; requiring an explicit non-`Created` code on every later
    // transition is what stops a lifecycle change being recorded as though it were the hire.
    if (reason == EmployeeStatusChangeReason.Created)
    {
      return Result.Failure(EmployeeErrors.InvalidTransitionReason);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    Status = next;
    StatusChangeReasonCode = reason;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    return Result.Success();
  }

  private static bool IsValidActor(string actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Trim().Length <= ActorMaximumLength;

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
