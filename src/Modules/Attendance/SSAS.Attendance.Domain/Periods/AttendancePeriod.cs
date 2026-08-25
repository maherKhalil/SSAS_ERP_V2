using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Periods;

// ================================================================================================
// THE ATTENDANCE PERIOD (REQ-ATT-0018, REQ-ATT-0019; OD-ATT-0010, OD-ATT-0012).
// ================================================================================================
//
// The unit Payroll consumes and the gate it passes through. `OD-ATT-0010` ruled (a): **periods close, and
// Payroll refuses an open one.**
//
// The reason is not tidiness. A payroll run calculated from a period still being edited is a snapshot of a
// moving target, and payroll runs are approved and POSTED TO GL — so a wrong snapshot becomes a posted
// journal entry, and reversing one of those is a business event rather than a fix.
//
// MUTABLE ITS WHOLE LIFE, exactly like `PayrollRun`: status transitions, audit stamps, `ClosedUtc`,
// `ClosedBy` and a `RowVersion`. It is the RECORDS that are append-only, not their container — and that
// separation is what makes the reopen action below safe rather than dangerous.
//
// NOT branch-owned. A period is a company-level accounting boundary; branch lives on the records inside it
// (`OD-ATT-0011`). `DEC-ATT-0014` requires the negative to be asserted, so an architecture test asserts this
// type does NOT implement `IBranchOwnedEntity`.
public enum AttendancePeriodStatus
{
  Open = 0,
  Closed = 1
}

public sealed class AttendancePeriodName : ValueObject
{
  public const int MaximumLength = 200;

  private AttendancePeriodName(string value) => Value = value;

  public string Value { get; }

  public static Result<AttendancePeriodName> Create(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Any(char.IsControl))
    {
      return Result.Failure<AttendancePeriodName>(AttendancePeriodErrors.InvalidName);
    }

    return Result.Success(new AttendancePeriodName(value.Trim()));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}

public sealed class AttendancePeriod
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private string normalizedName = string.Empty;

  private AttendancePeriod(
    Guid id, Guid companyId, AttendancePeriodName name, DateOnly startDate, DateOnly endDate)
    : base(id)
  {
    CompanyId = companyId;
    Name = name;
    normalizedName = name.Value.ToUpperInvariant();
    StartDate = startDate;
    EndDate = endDate;
    Status = AttendancePeriodStatus.Open;
  }

  // EF materialization only.
  private AttendancePeriod(Guid id)
    : base(id) => Name = null!;

  public Guid AttendancePeriodId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public AttendancePeriodName Name { get; private set; }

  public string NormalizedName => normalizedName;

  // Calendar days, not instants — the reasoning stated on `CalendarHoliday`. A period boundary that moved
  // across midnight under an offset conversion would silently move records between periods, and the periods
  // would still look correct.
  public DateOnly StartDate { get; private set; }

  public DateOnly EndDate { get; private set; }

  // ---- STORED AS A STRING, AND THIS IS A SCAR.
  //
  // FP-012's integration fixture seeded a company with an INTEGER status and `SYSUTCDATETIME()`, both copied
  // verbatim from GL's fixture and both wrong. Status enums in this codebase persist as strings. A fixture
  // that guesses the storage shape fails during SETUP, which reads as an environment problem rather than as
  // the fixture bug it is.
  public AttendancePeriodStatus Status { get; private set; }

  public DateTimeOffset? ClosedUtc { get; private set; }

  public string? ClosedBy { get; private set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[] RowVersion { get; private set; } = [];

  public bool IsClosed => Status == AttendancePeriodStatus.Closed;

  public bool Covers(DateOnly date) => date >= StartDate && date <= EndDate;

  public static Result<AttendancePeriod> Create(
    Guid companyId, string? name, DateOnly startDate, DateOnly endDate)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<AttendancePeriod>(AttendancePeriodErrors.CompanyRequired);
    }

    if (endDate < startDate)
    {
      return Result.Failure<AttendancePeriod>(AttendancePeriodErrors.InvalidRange);
    }

    var periodName = AttendancePeriodName.Create(name);
    if (periodName.IsFailure)
    {
      return Result.Failure<AttendancePeriod>(periodName.Error);
    }

    return Result.Success(new AttendancePeriod(Guid.NewGuid(), companyId, periodName.Value, startDate, endDate));
  }

  // ---- CLOSING IS A CHECKED ACT, NOT A FLAG FLIP (AC-ATT-0013).
  //
  // Refusing a second close matters more than it looks: a repeated close would overwrite `ClosedUtc` and
  // `ClosedBy`, silently rewriting who froze the numbers Payroll consumed.
  public Result Close(string? closedBy, DateTimeOffset closedUtc)
  {
    if (Status == AttendancePeriodStatus.Closed)
    {
      return Result.Failure(AttendancePeriodErrors.AlreadyClosed);
    }

    Status = AttendancePeriodStatus.Closed;
    ClosedUtc = closedUtc;
    ClosedBy = closedBy;
    return Result.Success();
  }

  // ================================================================================================
  // REOPEN — AND WHY IT SURVIVES THE OD-ATT-0012 RULING RATHER THAN CONTRADICTING IT.
  // ================================================================================================
  //
  // The analysis package drew this arrow as existing ONLY under ruling (b), and the owner ruled (a):
  // corrections are new adjustment records, never edits. So this action needs its justification stated
  // rather than assumed, because a reader who remembers the ruling will expect it to be absent.
  //
  // **Because `AttendanceRecord` is append-only FROM CREATION, reopening a period permits APPENDING and
  // never EDITING.** `TenantDbContext.PreventAppendOnlyMutation` refuses `Modified` and `Deleted` for any
  // `IAppendOnlyEntity` UNCONDITIONALLY — it does not consult period status, it has no escape hatch, and it
  // does not care who holds `Attendance.Periods.Close`. A reopened period cannot be used to rewrite history
  // by anyone, which is exactly why reopening is safe.
  //
  // Reopen is an administrative act that lets more facts arrive. It is not an eraser.
  //
  // And it does not silently invalidate a posted payroll journal either: `OD-ATT-0010` makes Payroll refuse
  // an OPEN period at approval, so reopening a period a run has already consumed blocks the NEXT approval
  // until it closes again, rather than quietly changing what a posted run was based on.
  public Result Reopen()
  {
    if (Status == AttendancePeriodStatus.Open)
    {
      return Result.Failure(AttendancePeriodErrors.AlreadyOpen);
    }

    Status = AttendancePeriodStatus.Open;
    ClosedUtc = null;
    ClosedBy = null;
    return Result.Success();
  }
}
