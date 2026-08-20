using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Departments;

// ONE IMMUTABLE RECORD OF WHICH DEPARTMENT AN EMPLOYEE BELONGED TO, AND FROM WHEN (REQ-HR-0006, ADR-026).
//
// It is the department counterpart of `EmployeeBranchAssignment`, and it follows that type's design
// deliberately rather than inventing a second history shape. A reader who knows one knows this.
//
// ================================================================================================
// IT IS DELIBERATELY **NOT** IBranchOwnedEntity. THIS IS THE CLASSIFICATION, NOT AN OMISSION.
// ================================================================================================
//
// A department change says nothing about a branch. The employee stays where they work; only their place in
// the org structure moves. Stamping a branch here would attach an unrelated dimension to the record and
// place an org-structure change inside the branch write boundary.
//
// ---- APPEND-ONLY, AND THAT IS THE WHOLE DESIGN.
//
// There are no mutators. There is no RowVersion, because a record that is never updated has no concurrency
// state to protect — concurrent changes are serialized by `Employee.RowVersion` instead, exactly as
// transfers are. There is no ModifiedUtc/ModifiedBy, because it is never modified. There is no
// EffectiveToUtc, because closing an interval would mean UPDATING the previous row, which is precisely the
// history mutation this model exists to prevent; the interval is derived by ordering.
//
// A correction is another department change, never a rewrite.
//
// ---- WHY THE SOURCE IS NULLABLE AND THE REASON CODE IS NOT AN ENUM.
//
// A null source is the INITIAL record — the employee's first department. `EmployeeBranchAssignment` pairs
// its nullable source with a mandatory `InitialAssignment` reason code and a check constraint tying the two
// together. This record's reason code is nullable free-form-bounded text rather than an enum, per the
// approved Phase 1 specification, so the equivalent constraint is expressed on the SOURCE alone: the
// initial record is the one with no source, and nothing else identifies it.
public sealed class EmployeeDepartmentAssignment
  : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;

  public const int ReasonCodeMaximumLength = 32;

  public const int ReasonTextMaximumLength = 512;

  private EmployeeDepartmentAssignment(
    Guid id,
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid? sourceDepartmentId,
    Guid destinationDepartmentId,
    DateTimeOffset effectiveFromUtc,
    string changedBy,
    string? reasonCode,
    string? reasonText) : base(id)
  {
    TenantId = tenantId;
    CompanyId = companyId;
    EmployeeId = employeeId;
    SourceDepartmentId = sourceDepartmentId;
    DestinationDepartmentId = destinationDepartmentId;
    EffectiveFromUtc = effectiveFromUtc.ToUniversalTime();
    ChangedBy = changedBy;
    ReasonCode = reasonCode;
    ReasonText = reasonText;
  }

  private EmployeeDepartmentAssignment()
    : base(Guid.Empty)
  {
    ChangedBy = string.Empty;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  // NULL ONLY ON THE INITIAL RECORD, written when an employee first receives a department. A check
  // constraint keeps it from equalling the destination, so a record can never describe a move to the
  // department it came from.
  public Guid? SourceDepartmentId { get; private set; }

  public Guid DestinationDepartmentId { get; private set; }

  // The commit instant, never a future value: V1 has no future-dated department change, which is what keeps
  // this monotonic per employee and the point-in-time query unambiguous without stored end dates.
  public DateTimeOffset EffectiveFromUtc { get; private set; }

  public string ChangedBy { get; private set; }

  // Bounded, and nullable. Persisted for the audit record; never used in a decision.
  public string? ReasonCode { get; private set; }

  // Persisted for the audit record ALONE. Never used in a decision, compared, indexed, or emitted in a
  // domain event — which is what keeps free text out of everything downstream.
  public string? ReasonText { get; private set; }

  // CreatedUtc/CreatedBy are owned by the IAuditableEntity persistence infrastructure. There is no Modified
  // pair: this record is never modified.
  public DateTimeOffset CreatedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  // THE INITIAL ASSIGNMENT, written in the same transaction as the department it records.
  //
  // `internal` in FP-006's equivalent, because the Employee aggregate produced it. Here it is `public`
  // because Phase 1 introduces no Employee change at all — the aggregate that will call this does not yet
  // reference departments — and an `internal` factory with no caller inside the assembly would be
  // unreachable and untestable. Phase 3 tightens it when Employee gains the operation.
  public static Result<EmployeeDepartmentAssignment> CreateInitial(
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid destinationDepartmentId,
    DateTimeOffset effectiveFromUtc,
    string actor)
  {
    if (employeeId == Guid.Empty || destinationDepartmentId == Guid.Empty)
    {
      return Result.Failure<EmployeeDepartmentAssignment>(DepartmentErrors.InvalidDepartmentAssignment);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeDepartmentAssignment>(DepartmentErrors.InvalidActor);
    }

    return Result.Success(new EmployeeDepartmentAssignment(
      Guid.NewGuid(), tenantId, companyId, employeeId,
      sourceDepartmentId: null,
      destinationDepartmentId,
      effectiveFromUtc,
      actor.Trim(),
      reasonCode: null,
      reasonText: null));
  }

  public static Result<EmployeeDepartmentAssignment> CreateChange(
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid sourceDepartmentId,
    Guid destinationDepartmentId,
    DateTimeOffset effectiveFromUtc,
    string actor,
    string? reasonCode,
    string? reasonText)
  {
    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeDepartmentAssignment>(DepartmentErrors.InvalidActor);
    }

    // A change to the department the employee is already in is not a change.
    if (employeeId == Guid.Empty ||
      sourceDepartmentId == Guid.Empty ||
      destinationDepartmentId == Guid.Empty ||
      sourceDepartmentId == destinationDepartmentId)
    {
      return Result.Failure<EmployeeDepartmentAssignment>(DepartmentErrors.InvalidDepartmentAssignment);
    }

    var trimmedCode = reasonCode?.Trim();
    if (trimmedCode is { Length: > ReasonCodeMaximumLength })
    {
      return Result.Failure<EmployeeDepartmentAssignment>(DepartmentErrors.InvalidDepartmentAssignment);
    }

    var trimmedText = reasonText?.Trim();
    if (trimmedText is { Length: > ReasonTextMaximumLength })
    {
      return Result.Failure<EmployeeDepartmentAssignment>(DepartmentErrors.InvalidDepartmentAssignment);
    }

    return Result.Success(new EmployeeDepartmentAssignment(
      Guid.NewGuid(), tenantId, companyId, employeeId,
      sourceDepartmentId, destinationDepartmentId, effectiveFromUtc, actor.Trim(),
      string.IsNullOrEmpty(trimmedCode) ? null : trimmedCode,
      string.IsNullOrEmpty(trimmedText) ? null : trimmedText));
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

  // ---- THE MODIFIED PAIR IS EXPLICITLY UNSUPPORTED.
  //
  // IAuditableEntity requires it, but this record is never modified, so the columns do not exist and the
  // setters are no-ops rather than fields nothing reads. The audit stamper writes them on Added entries and
  // this type is only ever Added; a getter returning CreatedUtc keeps the contract honest without implying
  // a modification that cannot happen.
  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => CreatedUtc;
    set { }
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => CreatedBy;
    set { }
  }
}
