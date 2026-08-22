using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// ONE IMMUTABLE RECORD OF WHICH POSITION AN EMPLOYEE HELD, AND FROM WHEN (REQ-HR-0006, DEC-POS-0008).
//
// It is the position counterpart of `EmployeeDepartmentAssignment`, and it follows that type's design
// deliberately rather than inventing a THIRD history shape. A reader who knows one knows this.
//
// ---- THE EXISTENCE OF THIS TABLE WAS NOT AN OPEN QUESTION.
//
// `DEC-DEP-0016` raised department history as `OD-DEP-004`, the owner REVERSED the deferral, and FP-007
// Phase 1 shipped it from the outset. The stated reason — that the deferral period is unrecoverable —
// applies to position history word for word, so re-raising it would have asked the owner to re-decide
// something they had already decided against in an identical case.
//
// ---- ONE DIFFERENCE FROM THE DEPARTMENT HISTORY, AND IT IS A CONSEQUENCE OF `OD-POS-001`.
//
// `20260820140653_AddEmployeeDepartment` backfilled every legacy employee into an `UNASSIGNED` department
// and wrote them each an initial record — so the department history carries a cohort whose first row
// describes a MIGRATION rather than a hiring decision. This table will carry no such cohort: the ruling
// creates no seeded position, and `DEC-POS-0026` makes the migration verify the premise rather than assume
// it. **Every row here will describe an assignment made through the application.**
//
// ================================================================================================
// IT IS DELIBERATELY **NOT** IBranchOwnedEntity. THIS IS THE CLASSIFICATION, NOT AN OMISSION.
// ================================================================================================
//
// A position change says nothing about a branch. The employee stays where they work; only their job moves.
// Stamping a branch here would attach an unrelated dimension to the record and place an org-structure change
// inside the branch write boundary.
//
// ---- APPEND-ONLY, AND THAT IS THE WHOLE DESIGN.
//
// There are no mutators. There is no RowVersion, because a record that is never updated has no concurrency
// state to protect — concurrent changes are serialized by `Employee.RowVersion` instead, exactly as
// transfers and department changes are. There is no ModifiedUtc/ModifiedBy, because it is never modified.
// There is no EffectiveToUtc, because closing an interval would mean UPDATING the previous row, which is
// precisely the history mutation this model exists to prevent; the interval is derived by ordering.
//
// A correction is another position change, never a rewrite.
//
// ---- WHY THE SOURCE IS NULLABLE.
//
// A null source is the INITIAL record — the employee's first position. A check constraint keeps the source
// from equalling the destination, so a record can never describe a move to the position it came from, and
// nothing else identifies the initial row.
public sealed class EmployeePositionAssignment
  : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;

  public const int ReasonCodeMaximumLength = 32;

  public const int ReasonTextMaximumLength = 512;

  private EmployeePositionAssignment(
    Guid id,
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid? sourcePositionId,
    Guid destinationPositionId,
    DateTimeOffset effectiveFromUtc,
    string changedBy,
    string? reasonCode,
    string? reasonText) : base(id)
  {
    TenantId = tenantId;
    CompanyId = companyId;
    EmployeeId = employeeId;
    SourcePositionId = sourcePositionId;
    DestinationPositionId = destinationPositionId;
    EffectiveFromUtc = effectiveFromUtc.ToUniversalTime();
    ChangedBy = changedBy;
    ReasonCode = reasonCode;
    ReasonText = reasonText;
  }

  private EmployeePositionAssignment()
    : base(Guid.Empty)
  {
    ChangedBy = string.Empty;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  // NULL ONLY ON THE INITIAL RECORD, written when an employee first receives a position.
  public Guid? SourcePositionId { get; private set; }

  public Guid DestinationPositionId { get; private set; }

  // The commit instant, never a future value: V1 has no future-dated position change, which is what keeps
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

  // ---- THE FACTORIES ARE `internal`, AND THAT IS THE POINT (DEC-POS-0008).
  //
  // The Employee aggregate is the only thing that may produce a history row, so nothing outside this
  // assembly can fabricate one. FP-007's Phase 1 left its equivalents `public` because Employee did not yet
  // reference departments and an internal factory with no caller would have been unreachable and untestable;
  // Phase 3 then narrowed them.
  //
  // **This phase does not repeat that.** The factories are `internal` from the outset. HR.Tests reaches them
  // through `InternalsVisibleTo`, so the guards below are proven now rather than on the promise of a later
  // narrowing — and there is no window in which a wider surface exists to be depended on.
  internal static Result<EmployeePositionAssignment> CreateInitial(
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid destinationPositionId,
    DateTimeOffset effectiveFromUtc,
    string actor)
  {
    if (employeeId == Guid.Empty || destinationPositionId == Guid.Empty)
    {
      return Result.Failure<EmployeePositionAssignment>(PositionErrors.InvalidPositionAssignment);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeePositionAssignment>(PositionErrors.InvalidActor);
    }

    return Result.Success(new EmployeePositionAssignment(
      Guid.NewGuid(), tenantId, companyId, employeeId,
      sourcePositionId: null,
      destinationPositionId,
      effectiveFromUtc,
      actor.Trim(),
      reasonCode: null,
      reasonText: null));
  }

  internal static Result<EmployeePositionAssignment> CreateChange(
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid sourcePositionId,
    Guid destinationPositionId,
    DateTimeOffset effectiveFromUtc,
    string actor,
    string? reasonCode,
    string? reasonText)
  {
    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeePositionAssignment>(PositionErrors.InvalidActor);
    }

    // A change to the position the employee already holds is not a change.
    if (employeeId == Guid.Empty ||
      sourcePositionId == Guid.Empty ||
      destinationPositionId == Guid.Empty ||
      sourcePositionId == destinationPositionId)
    {
      return Result.Failure<EmployeePositionAssignment>(PositionErrors.InvalidPositionAssignment);
    }

    var trimmedCode = reasonCode?.Trim();
    if (trimmedCode is { Length: > ReasonCodeMaximumLength })
    {
      return Result.Failure<EmployeePositionAssignment>(PositionErrors.InvalidPositionAssignment);
    }

    var trimmedText = reasonText?.Trim();
    if (trimmedText is { Length: > ReasonTextMaximumLength })
    {
      return Result.Failure<EmployeePositionAssignment>(PositionErrors.InvalidPositionAssignment);
    }

    return Result.Success(new EmployeePositionAssignment(
      Guid.NewGuid(), tenantId, companyId, employeeId,
      sourcePositionId, destinationPositionId, effectiveFromUtc, actor.Trim(),
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
  // this type is only ever Added; a getter returning CreatedUtc keeps the contract honest without implying a
  // modification that cannot happen.
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
