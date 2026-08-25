using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Records;

// ================================================================================================
// THE ATOM (REQ-ATT-0004, REQ-ATT-0007, REQ-ATT-0008; OD-ATT-0003, OD-ATT-0011, OD-ATT-0012).
// ================================================================================================
//
// `OD-ATT-0003` ruled DAILY RECORDS, not clock events. One employee, one date, the quantities observed.
//
// Clock events were refused for two reasons worth keeping: they are materially more sensitive — a record of
// when a person came and went, rather than that they worked eight hours — and they force a missed-punch
// repair workflow into v1 for a benefit payroll never consumes.
//
// ================================================================================================
// APPEND-ONLY FROM CREATION, AND THE MECHANISM DECIDES THE SHAPE (DEC-ATT-0009, OD-ATT-0012).
// ================================================================================================
//
// `TenantDbContext.PreventAppendOnlyMutation` refuses `EntityState.Modified` **or** `EntityState.Deleted`
// for any `IAppendOnlyEntity` — **UNCONDITIONALLY**. No status check, no flag, no escape hatch.
//
// FP-012 learned what that means the hard way. A payroll run needs mutable lines while it is a draft and
// immutable lines once approved, and NO SINGLE TYPE CAN BE BOTH, because the refusal does not consult
// status. The answer there was three types.
//
// Here it is one type, because `OD-ATT-0012` ruled **adjustments, never edits**. A record is a statement
// about a past date from the moment it is made, so there is no draft phase to model and no second type to
// invent. The ruling and the mechanism agree, which is why this aggregate is simple where `PayrollRun` is
// not.
//
// **Two consequences follow, and both are easy to get wrong:**
//
//   1. NO `RowVersion`. An append-only row has nothing to concurrency-check, and the column would imply an
//      update path exists.
//
//   2. NO UNIQUE INDEX on (TenantId, EmployeeId, AttendanceDate). A second row for the same employee-date
//      **is exactly what an adjustment is**. The analysis package flagged this as the sharpest coupling in
//      the data model: an index chosen from the happy path silently forecloses the correction model.
//
// ================================================================================================
// BRANCH-OWNED — THE SUPERVISOR HALF OF OD-ATT-0011'S SPLIT.
// ================================================================================================
//
// Attendance is observed LOCALLY. A branch supervisor records who was present at their branch and has no
// business reading another's, and the whole `UserBranchAccess` to `ITenantBranchAccessResolver` stack exists
// so that boundary is enforced rather than trusted.
//
// `BranchId` is public-set because `IBranchOwnedEntity` requires it: the branch write boundary stamps it
// from the execution context via `ICurrentBranchResolver`, and a caller never supplies it. Everything else
// here has a private setter.
//
// **The other half of the split lives in `IAttendanceSummary`, which is branch-BLIND on purpose.** A
// supervisor cannot read another branch's records; Payroll reads every branch's hours in a company total.
// The hole is ruled INTENDED (`OD-ATT-0011`), because payroll is a company-level act and a branch filter on
// the payroll path is how `DEC-PAY-0017` says employees get silently omitted.

// ---- WHAT A ROW IS SAYING.
//
// The distinction is not cosmetic: it decides whether negative quantities are legal.
public enum AttendanceRecordKind
{
  // What was observed on the day. Quantities are non-negative — you cannot work minus three hours.
  Observation = 0,

  // ---- A CORRECTION TO SOMETHING ALREADY RECORDED (OD-ATT-0012, the GL model).
  //
  // Carries DELTAS, which may be negative, and names the record it corrects. The original is never touched;
  // the truth for an employee-date is the SUM of its observation and every adjustment against it — exactly
  // how a correcting journal entry works, and for the same reason.
  //
  // An adjustment to a CLOSED period's date lands in the current OPEN period while keeping the original
  // `AttendanceDate`. The date says when it happened; the period says when it was recorded. That separation
  // is what lets a closed period stay closed while the record of what actually happened stays correct.
  Adjustment = 1
}

public sealed class AttendanceRecord
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IBranchOwnedEntity, IAppendOnlyEntity
{
  public const int OvertimeTierMaximumLength = 32;
  public const int NoteMaximumLength = 1000;

  private AttendanceRecord(
    Guid id,
    Guid companyId,
    Guid attendancePeriodId,
    Guid employeeId,
    DateOnly attendanceDate,
    AttendanceRecordKind kind,
    Guid? adjustedRecordId,
    decimal workedQuantity,
    decimal overtimeQuantity,
    string? overtimeTier,
    decimal paidAbsenceQuantity,
    decimal unpaidAbsenceQuantity,
    string? note)
    : base(id)
  {
    CompanyId = companyId;
    AttendancePeriodId = attendancePeriodId;
    EmployeeId = employeeId;
    AttendanceDate = attendanceDate;
    Kind = kind;
    AdjustedRecordId = adjustedRecordId;
    WorkedQuantity = workedQuantity;
    OvertimeQuantity = overtimeQuantity;
    OvertimeTier = overtimeTier;
    PaidAbsenceQuantity = paidAbsenceQuantity;
    UnpaidAbsenceQuantity = unpaidAbsenceQuantity;
    Note = note;
  }

  // EF materialization only.
  private AttendanceRecord(Guid id)
    : base(id)
  {
  }

  public Guid AttendanceRecordId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  // Public-set because `IBranchOwnedEntity` requires it for stamping. See the header: the write boundary
  // supplies this from the execution context, never the caller.
  public Guid BranchId { get; set; }

  public Guid AttendancePeriodId { get; private set; }

  // Held as a value. The employment-window check (`REQ-ATT-0006`) runs against `IEmployeeRoster` at write
  // time rather than as a constraint, because the dates live in HR's tables and reaching them directly would
  // be the module coupling `ADR-012` forbids. The shared Tenant DB makes it POSSIBLE, which is exactly why
  // the rule has to be deliberate.
  public Guid EmployeeId { get; private set; }

  // A calendar day, not an instant — the reasoning stated on `CalendarHoliday`.
  public DateOnly AttendanceDate { get; private set; }

  public AttendanceRecordKind Kind { get; private set; }

  // Set only on an adjustment. Names the observation being corrected, so a reader can reconstruct what was
  // originally claimed as well as what is now true.
  public Guid? AdjustedRecordId { get; private set; }

  // ---- QUANTITIES, NOT MONEY (DEC-ATT-0004, REQ-ATT-0009).
  //
  // `decimal(9,2)` in the schema, deliberately NOT the `decimal(19,4)` money type, so the two are
  // distinguishable at a glance and an integration test can assert that no Attendance column uses the money
  // shape. Hours are not integers; a quantity is still not money.
  public decimal WorkedQuantity { get; private set; }

  // `OD-ATT-0008` ruled overtime RECORDED with a tier label. The MULTIPLIER lives in Payroll as a pay
  // element, because a multiplier is a rate and rates are money. Attendance says "three hours at tier
  // NIGHT"; Payroll decides what tier NIGHT is worth.
  public decimal OvertimeQuantity { get; private set; }

  public string? OvertimeTier { get; private set; }

  public decimal PaidAbsenceQuantity { get; private set; }

  // The one Payroll deducts, and the reason `DEC-PAY-0002`'s absence-deduction behaviour could not exist
  // before this module.
  public decimal UnpaidAbsenceQuantity { get; private set; }

  public string? Note { get; private set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  // Present because `IAuditableEntity` requires them. They are stamped once at creation and never move
  // again: the write boundary refuses a `Modified` entry for this type, so there is no second write for
  // them to record. Kept rather than fought because the interface is the contract with the context.
  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public static Result<AttendanceRecord> Observe(
    Guid companyId,
    Guid attendancePeriodId,
    Guid employeeId,
    DateOnly attendanceDate,
    decimal workedQuantity,
    decimal overtimeQuantity,
    string? overtimeTier,
    decimal paidAbsenceQuantity,
    decimal unpaidAbsenceQuantity,
    string? note)
  {
    var validated = Validate(companyId, attendancePeriodId, employeeId, overtimeTier, note);
    if (validated.IsFailure)
    {
      return Result.Failure<AttendanceRecord>(validated.Error);
    }

    // An observation cannot be negative. This is the whole reason `AttendanceRecordKind` exists: without the
    // distinction, either corrections would be impossible or every quantity in the module would have to
    // accept a negative and nothing would catch a mis-keyed one.
    if (workedQuantity < 0m || overtimeQuantity < 0m || paidAbsenceQuantity < 0m || unpaidAbsenceQuantity < 0m)
    {
      return Result.Failure<AttendanceRecord>(AttendanceRecordErrors.NegativeObservation);
    }

    if (overtimeQuantity > 0m && string.IsNullOrWhiteSpace(overtimeTier))
    {
      return Result.Failure<AttendanceRecord>(AttendanceRecordErrors.OvertimeTierRequired);
    }

    return Result.Success(new AttendanceRecord(
      Guid.NewGuid(), companyId, attendancePeriodId, employeeId, attendanceDate,
      AttendanceRecordKind.Observation, adjustedRecordId: null,
      workedQuantity, overtimeQuantity, Trim(overtimeTier),
      paidAbsenceQuantity, unpaidAbsenceQuantity, Trim(note)));
  }

  // ---- THE CORRECTION PATH (REQ-ATT-0019, OD-ATT-0012).
  //
  // Deltas, signed. A note is REQUIRED: an adjustment without a reason is a number that changes what someone
  // is paid with nothing recorded about why, and this is the one place in the module where that matters
  // enough to enforce.
  public static Result<AttendanceRecord> Adjust(
    Guid companyId,
    Guid attendancePeriodId,
    Guid employeeId,
    DateOnly attendanceDate,
    Guid adjustedRecordId,
    decimal workedDelta,
    decimal overtimeDelta,
    string? overtimeTier,
    decimal paidAbsenceDelta,
    decimal unpaidAbsenceDelta,
    string? note)
  {
    var validated = Validate(companyId, attendancePeriodId, employeeId, overtimeTier, note);
    if (validated.IsFailure)
    {
      return Result.Failure<AttendanceRecord>(validated.Error);
    }

    if (adjustedRecordId == Guid.Empty)
    {
      return Result.Failure<AttendanceRecord>(AttendanceRecordErrors.AdjustedRecordRequired);
    }

    if (string.IsNullOrWhiteSpace(note))
    {
      return Result.Failure<AttendanceRecord>(AttendanceRecordErrors.AdjustmentNoteRequired);
    }

    if (workedDelta == 0m && overtimeDelta == 0m && paidAbsenceDelta == 0m && unpaidAbsenceDelta == 0m)
    {
      return Result.Failure<AttendanceRecord>(AttendanceRecordErrors.AdjustmentChangesNothing);
    }

    if (overtimeDelta != 0m && string.IsNullOrWhiteSpace(overtimeTier))
    {
      return Result.Failure<AttendanceRecord>(AttendanceRecordErrors.OvertimeTierRequired);
    }

    return Result.Success(new AttendanceRecord(
      Guid.NewGuid(), companyId, attendancePeriodId, employeeId, attendanceDate,
      AttendanceRecordKind.Adjustment, adjustedRecordId,
      workedDelta, overtimeDelta, Trim(overtimeTier),
      paidAbsenceDelta, unpaidAbsenceDelta, Trim(note)));
  }

  private static Result Validate(
    Guid companyId, Guid attendancePeriodId, Guid employeeId, string? overtimeTier, string? note)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure(AttendanceRecordErrors.CompanyRequired);
    }

    if (attendancePeriodId == Guid.Empty)
    {
      return Result.Failure(AttendanceRecordErrors.PeriodRequired);
    }

    if (employeeId == Guid.Empty)
    {
      return Result.Failure(AttendanceRecordErrors.EmployeeRequired);
    }

    if (overtimeTier is not null &&
      (overtimeTier.Length > OvertimeTierMaximumLength || overtimeTier.Any(char.IsControl)))
    {
      return Result.Failure(AttendanceRecordErrors.InvalidOvertimeTier);
    }

    if (note is not null && (note.Length > NoteMaximumLength || note.Any(char.IsControl)))
    {
      return Result.Failure(AttendanceRecordErrors.InvalidNote);
    }

    return Result.Success();
  }

  private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
