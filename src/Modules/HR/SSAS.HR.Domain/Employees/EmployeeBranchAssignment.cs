using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Employees;

// ONE IMMUTABLE RECORD OF WHERE AN EMPLOYEE WORKED, AND FROM WHEN (FP-006, ADR-024 decisions 4 and 5).
//
// ================================================================================================
// IT IS DELIBERATELY **NOT** IBranchOwnedEntity. THIS IS THE CLASSIFICATION, NOT AN OMISSION.
// ================================================================================================
//
// A transfer record SPANS a branch boundary: it names the branch left and the branch entered, and belongs
// to neither. Stamping it with a single BranchId would either hide a departure from the branch that
// received the employee or hide an arrival from the branch that released them — and it would collide with
// the write boundary, whose trusted context during a transfer is the SOURCE while this record's subject is
// the DESTINATION.
//
// So it carries the tenant and company dimensions, which it genuinely belongs to, and treats both branch
// identifiers as ATTRIBUTES. Neither is named `BranchId`, precisely so that no future convention, shadow
// property or interface implementation can silently reclassify this type as branch-owned (ADR-024 decision
// 4; FP-006 TS-EMP-0113).
//
// ---- APPEND-ONLY, AND THAT IS THE WHOLE DESIGN.
//
// There are no mutators. There is no RowVersion, because a record that is never updated has no concurrency
// state to protect — concurrent transfers are serialized by Employee.RowVersion instead. There is no
// ModifiedUtc/ModifiedBy, because it is never modified. There is no EffectiveToUtc, because closing an
// interval would mean UPDATING the previous row, which is exactly the history mutation this model exists to
// prevent; the interval is derived by ordering instead, which V1's ban on future-dating makes unambiguous.
//
// A correction is another transfer, never a rewrite.
public sealed class EmployeeBranchAssignment
  : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;

  public const int ReasonTextMaximumLength = 512;

  private EmployeeBranchAssignment(
    Guid id,
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid? sourceBranchId,
    Guid destinationBranchId,
    DateTimeOffset effectiveFromUtc,
    string transferredBy,
    EmployeeBranchTransferReason reasonCode,
    string? reasonText) : base(id)
  {
    TenantId = tenantId;
    CompanyId = companyId;
    EmployeeId = employeeId;
    SourceBranchId = sourceBranchId;
    DestinationBranchId = destinationBranchId;
    EffectiveFromUtc = effectiveFromUtc.ToUniversalTime();
    TransferredBy = transferredBy;
    ReasonCode = reasonCode;
    ReasonText = reasonText;
  }

  private EmployeeBranchAssignment()
    : base(Guid.Empty)
  {
    TransferredBy = string.Empty;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  // NULL ONLY ON THE INITIAL RECORD written at Employee creation. A check constraint pairs this with
  // ReasonCode so the initial record and a transfer record can never be confused for one another.
  public Guid? SourceBranchId { get; private set; }

  public Guid DestinationBranchId { get; private set; }

  // The commit instant, never a future value: V1 has no future-dated transfer, which is what keeps this
  // monotonic per employee and the point-in-time query unambiguous without stored end dates.
  public DateTimeOffset EffectiveFromUtc { get; private set; }

  public string TransferredBy { get; private set; }

  public EmployeeBranchTransferReason ReasonCode { get; private set; }

  // Persisted for the audit record ALONE. It is never used in a decision, compared, indexed, or emitted in
  // a domain event — which is what keeps free text out of everything downstream.
  public string? ReasonText { get; private set; }

  // CreatedUtc/CreatedBy are owned by the IAuditableEntity persistence infrastructure and are never stamped
  // by the Domain. There is no Modified pair: this record is never modified.
  public DateTimeOffset CreatedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  // THE INITIAL ASSIGNMENT, written in the same transaction as the Employee it describes. An Employee with
  // no branch history is a defect, so this is produced by the aggregate's own factory rather than left to a
  // caller to remember.
  internal static Result<EmployeeBranchAssignment> CreateInitial(
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid destinationBranchId,
    DateTimeOffset effectiveFromUtc,
    string actor)
  {
    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.InvalidActor);
    }

    return Result.Success(new EmployeeBranchAssignment(
      Guid.NewGuid(), tenantId, companyId, employeeId,
      sourceBranchId: null,
      destinationBranchId,
      effectiveFromUtc,
      actor.Trim(),
      EmployeeBranchTransferReason.InitialAssignment,
      reasonText: null));
  }

  internal static Result<EmployeeBranchAssignment> CreateTransfer(
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    Guid sourceBranchId,
    Guid destinationBranchId,
    DateTimeOffset effectiveFromUtc,
    string actor,
    EmployeeBranchTransferReason reasonCode,
    string? reasonText)
  {
    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.InvalidActor);
    }

    // `InitialAssignment` belongs to creation alone. Allowing it here would let a transfer masquerade as the
    // record of a hire, which is the one distinction the history has to keep straight.
    if (reasonCode == EmployeeBranchTransferReason.InitialAssignment)
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.InvalidTransferReason);
    }

    if (sourceBranchId == Guid.Empty || destinationBranchId == Guid.Empty ||
      sourceBranchId == destinationBranchId)
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.TransferDestinationUnchanged);
    }

    var trimmedReason = reasonText?.Trim();
    if (trimmedReason is { Length: > ReasonTextMaximumLength })
    {
      return Result.Failure<EmployeeBranchAssignment>(EmployeeErrors.InvalidTransferReason);
    }

    return Result.Success(new EmployeeBranchAssignment(
      Guid.NewGuid(), tenantId, companyId, employeeId,
      sourceBranchId, destinationBranchId, effectiveFromUtc, actor.Trim(), reasonCode,
      string.IsNullOrEmpty(trimmedReason) ? null : trimmedReason));
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
