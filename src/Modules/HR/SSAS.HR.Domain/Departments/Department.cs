using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Events;

namespace SSAS.HR.Domain.Departments;

// THE FIRST HIERARCHICAL AGGREGATE IN THE PRODUCT (REQ-HR-0100/0101/0102, ADR-026).
//
// ================================================================================================
// IT IS DELIBERATELY **NOT** IBranchOwnedEntity. THIS IS THE CLASSIFICATION, NOT AN OMISSION.
// ================================================================================================
//
// Employee implements all three ownership dimensions, so a Department beside it with two reads as an
// oversight until something says otherwise. `BR-PLT-0013` scopes branch ownership to TRANSACTIONS, and a
// department is a master organizational record. The decisive argument is `ADR-024`: employee branch
// transfer is a sanctioned branch-only operation, so a branch-owned department would strand the employee's
// department on every transfer and break `BR-HR-0005` — and `ADR-024` provides for nothing of the kind.
//
// A department therefore SPANS the branches of its company: "Sales" exists once and has people in several
// locations. An architecture guard asserts the absence of `IBranchOwnedEntity` and of any `BranchId`
// property, so no future convention can silently reclassify it.
//
// ---- WHAT THE AGGREGATE DOES NOT LET YOU DO.
//
// There is no setter for `TenantId` or `CompanyId` beyond the ownership interfaces the persistence layer
// stamps through. `Status` cannot be assigned — only `Deactivate()` and `Reactivate()`, each of which
// records who and when. `ParentDepartmentId` cannot be assigned — only `ChangeParent()`.
//
// ---- WHAT PHASE 1 DELIBERATELY DOES NOT ENFORCE.
//
// `ChangeParent` here refuses exactly one thing: a department being its own parent. That is the only part
// of `BR-HR-0008` decidable without I/O. Cross-company parents, inactive parents and the general
// descendant-as-parent cycle all require an ancestry walk the aggregate cannot perform, and they are
// Phase 2. **Nothing here pretends to enforce them**, because a half-enforced invariant that looks complete
// is worse than one that visibly is not.
public sealed class Department
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private string normalizedCode = string.Empty;

  // The search form of the name, maintained beside the code's (`DEC-POS-0030`). Set in exactly the two
  // places `normalizedCode` is set, so the two can never disagree about which write produced them.
  //
  // It arrived in FP-008 Phase 2 rather than with the aggregate, because FP-007's search filtered on the
  // value-converted `Name` and therefore never ran at all — see `DepartmentName.NormalizedValue`.
  private string normalizedName = string.Empty;

  private Department(
    Guid departmentId,
    DepartmentCode code,
    DepartmentName name,
    Guid? parentDepartmentId,
    string actor,
    DateTimeOffset occurredUtc) : base(departmentId)
  {
    Code = code;
    normalizedCode = code.NormalizedValue;
    normalizedName = name.NormalizedValue;
    Name = name;
    ParentDepartmentId = parentDepartmentId;
    Status = DepartmentStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
  }

  private Department()
    : base(Guid.Empty)
  {
    Code = null!;
    Name = null!;
    StatusChangedBy = string.Empty;
  }

  public Guid DepartmentId => Id;

  // ---- THE TWO OWNERSHIP DIMENSIONS.
  //
  // Both are stamped by the shared persistence boundaries from trusted server context, never by a caller.
  // The interface setters exist for that stamping and for nothing else.
  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public DepartmentCode Code { get; private set; }

  public string NormalizedCode => normalizedCode;

  public string NormalizedName => normalizedName;

  public DepartmentName Name { get; private set; }

  // NULL MEANS ROOT. A company may have more than one root: requiring a single one would force an
  // artificial "Company" node into every hierarchy.
  public Guid? ParentDepartmentId { get; private set; }

  public DepartmentStatus Status { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; }

  // CreatedUtc/CreatedBy/ModifiedUtc/ModifiedBy are owned by the IAuditableEntity persistence
  // infrastructure and are never stamped by the Domain.
  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  // ---- CREATE.
  //
  // The parent is accepted as an identifier and validated only for the self-reference case — which cannot
  // arise here, since the identifier is generated after the check would run. It is nonetheless refused
  // symmetrically with `ChangeParent` so the rule reads the same in both places.
  public static Result<Department> Create(
    DepartmentCode code,
    DepartmentName name,
    Guid? parentDepartmentId,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure<Department>(DepartmentErrors.InvalidCode);
    }

    if (name is null)
    {
      return Result.Failure<Department>(DepartmentErrors.InvalidName);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Department>(DepartmentErrors.InvalidActor);
    }

    if (parentDepartmentId == Guid.Empty)
    {
      return Result.Failure<Department>(DepartmentErrors.InvalidParent);
    }

    return Result.Success(new Department(
      Guid.NewGuid(), code, name, parentDepartmentId, actor.Trim(), occurredUtc));
  }

  // Called by the application once the trusted tenant and company are known — which is after the aggregate
  // exists, because both come from the execution context rather than from the caller's input.
  public Result StampCreated(Guid tenantId, Guid companyId, Guid eventId, DateTimeOffset occurredUtc)
  {
    RaiseDomainEvent(new DepartmentCreated(
      eventId, occurredUtc, Id, tenantId, companyId, ParentDepartmentId, DepartmentStatus.Active));

    return Result.Success();
  }

  // ---- RENAME AND RECODE.
  //
  // Only the descriptive fields. Ownership, status and parent are absent from this operation by
  // construction, not by validation — each has its own.
  public Result UpdateDescription(
    DepartmentCode code, DepartmentName name, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure(DepartmentErrors.InvalidCode);
    }

    if (name is null)
    {
      return Result.Failure(DepartmentErrors.InvalidName);
    }

    Code = code;
    normalizedCode = code.NormalizedValue;
    normalizedName = name.NormalizedValue;
    Name = name;

    RaiseDomainEvent(new DepartmentDescriptionUpdated(eventId, occurredUtc, Id, TenantId, CompanyId));

    return Result.Success();
  }

  // ---- HIERARCHY.
  //
  // PHASE 1 REFUSES SELF-PARENT AND NOTHING MORE. The general acyclicity rule needs ancestry evidence the
  // aggregate cannot obtain, and `ADR-026` decision 4 specifies that evidence as a value only the
  // repository can produce — so that a handler which skipped the check would not compile. That signature
  // change arrives with Phase 2; until then this method is reachable only from Phase 1's own tests, and no
  // application handler calls it.
  //
  // The database carries `CK_Departments_ParentIsNotSelf` for the same case, so the one part of
  // `BR-HR-0008` expressible as a constraint is enforced even against direct SQL.
  public Result ChangeParent(Guid? newParentDepartmentId, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (newParentDepartmentId == Id)
    {
      return Result.Failure(DepartmentErrors.ParentIsSelf);
    }

    if (newParentDepartmentId == Guid.Empty)
    {
      return Result.Failure(DepartmentErrors.InvalidParent);
    }

    var previousParentId = ParentDepartmentId;
    ParentDepartmentId = newParentDepartmentId;

    RaiseDomainEvent(new DepartmentParentChanged(
      eventId, occurredUtc, Id, TenantId, CompanyId, previousParentId, newParentDepartmentId));

    return Result.Success();
  }

  // ---- LIFECYCLE. Active <-> Inactive, reversibly.
  //
  // Neither method consults children or employees. "Refuse deactivation while active children remain" is a
  // cross-aggregate rule requiring a repository lookup, and it belongs to the application orchestration in
  // Phase 2 — stated here so its absence reads as sequencing rather than as an oversight.
  public Result Deactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != DepartmentStatus.Active)
    {
      return Result.Failure(DepartmentErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(DepartmentErrors.InvalidActor);
    }

    Status = DepartmentStatus.Inactive;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new DepartmentDeactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId, DepartmentStatus.Active, DepartmentStatus.Inactive));

    return Result.Success();
  }

  public Result Reactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != DepartmentStatus.Inactive)
    {
      return Result.Failure(DepartmentErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(DepartmentErrors.InvalidActor);
    }

    Status = DepartmentStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new DepartmentReactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId, DepartmentStatus.Inactive, DepartmentStatus.Active));

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
