using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;

namespace SSAS.Attendance.Application.Abstractions;

// ATTENDANCE'S WRITE-SIDE PORTS.
//
// One interface per aggregate root, each exposing only what its handlers need. The absences are as
// deliberate as the presences and are noted wherever a reader might expect a method.
public interface IWorkingCalendarRepository
{
  // Loads WITH holidays. There is no without-holidays overload, and that is the opposite choice from
  // `IPayrollRunRepository`'s three loaders — for the opposite reason. A run's approved lines are an entire
  // company's pay history and must not be loaded to answer a status question; a calendar's holidays are a
  // few dozen rows and **every** operation on a calendar needs them, because `IsWorkingDay` is meaningless
  // without them. A loader that omitted them would silently answer "yes, a working day" for every holiday.
  Task<WorkingCalendar?> GetByIdAsync(Guid workingCalendarId, CancellationToken cancellationToken = default);

  // The calendar a company's leave and attendance are computed against. Returns the default one.
  Task<WorkingCalendar?> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

  Task<bool> NameExistsAsync(Guid companyId, string normalizedName, CancellationToken cancellationToken = default);

  Task AddAsync(WorkingCalendar calendar, CancellationToken cancellationToken = default);

  // ================================================================================================
  // REMOVING A HOLIDAY DELETES THE ROW EXPLICITLY (FP-013 follow-up).
  // ================================================================================================
  //
  // `WorkingCalendar.RemoveHoliday` takes the holiday out of the collection, and
  // `WorkingCalendarConfiguration` asks for `DeleteBehavior.Cascade` and does not get it:
  // `PersistenceDbContext.OnModelCreating` sets EVERY foreign key in the composed model to `Restrict` AFTER
  // the module contributors run. Deliberate platform policy — no silent cascades in a multi-tenant model —
  // and `TenantDbContext` names it where the contributors are applied.
  //
  // So a removed holiday is an orphan nothing deletes, against a non-nullable foreign key EF cannot null,
  // and the save fails. **The same defect Payroll and GL carry, in the module written after both of them**
  // — which is how a convention that lives only in comments spreads instead of being enforced.
  Task RemoveHolidayAsync(CalendarHoliday holiday, CancellationToken cancellationToken = default);
}

public interface IAttendancePeriodRepository
{
  Task<AttendancePeriod?> GetByIdAsync(Guid attendancePeriodId, CancellationToken cancellationToken = default);

  // The period covering a date, whatever its status. Used both by the write path — which then refuses a
  // closed one — and by `IAttendanceSummary`, which reports the status rather than refusing.
  //
  // Returning the period regardless of status is what lets the summary contract answer `PeriodOpen` as a
  // MODELLED OUTCOME instead of throwing. A repository that filtered to open periods would make
  // `PeriodOpen` and `PeriodNotFound` indistinguishable, and the two have different remedies.
  Task<AttendancePeriod?> GetCoveringAsync(
    Guid companyId, DateOnly onDate, CancellationToken cancellationToken = default);

  // The open period a correction to a closed one lands in (`OD-ATT-0012`). Distinct from `GetCoveringAsync`
  // because an adjustment's PERIOD and its DATE deliberately differ: the date says when it happened, the
  // period says when it was recorded.
  Task<AttendancePeriod?> GetCurrentOpenAsync(
    Guid companyId, DateOnly asOf, CancellationToken cancellationToken = default);

  Task<bool> OverlapsAsync(
    Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

  Task AddAsync(AttendancePeriod period, CancellationToken cancellationToken = default);
}

public interface IAttendanceRecordRepository
{
  Task<AttendanceRecord?> GetByIdAsync(Guid attendanceRecordId, CancellationToken cancellationToken = default);

  // Every record for one employee in one period — observations and adjustments together, because the truth
  // for an employee-date is their SUM. A method returning only observations would be a trap: it would look
  // like the answer and silently omit every correction.
  Task<IReadOnlyList<AttendanceRecord>> GetForEmployeePeriodAsync(
    Guid attendancePeriodId, Guid employeeId, CancellationToken cancellationToken = default);

  Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken = default);

  // ---- THERE IS NO Update, AND NO Remove.
  //
  // `AttendanceRecord` is `IAppendOnlyEntity`, and `PreventAppendOnlyMutation` refuses `Modified` or
  // `Deleted` for it UNCONDITIONALLY. The methods are absent so the port cannot express what the write
  // boundary would refuse — the rule enforced by the shape of the interface rather than by everyone
  // remembering it.
  //
  // A correction is an `AddAsync` of an adjustment. That is the whole of `OD-ATT-0012`.
}

public interface ILeaveTypeRepository
{
  Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsAsync(Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  Task AddAsync(LeaveType leaveType, CancellationToken cancellationToken = default);
}

public interface ILeaveBalanceRepository
{
  Task<LeaveBalance?> GetByIdAsync(Guid leaveBalanceId, CancellationToken cancellationToken = default);

  // The balance a request consumes. Null is a legitimate answer meaning no entitlement was ever
  // administered, which `OD-ATT-0006` makes a refusal rather than an implicit zero — an implicit zero would
  // let an unadministered employee's request fail with "insufficient balance" when the truth is that nobody
  // set one up.
  Task<LeaveBalance?> GetForEmployeeAsync(
    Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear, CancellationToken cancellationToken = default);

  Task AddAsync(LeaveBalance balance, CancellationToken cancellationToken = default);
}

public interface ILeaveRequestRepository
{
  Task<LeaveRequest?> GetByIdAsync(Guid leaveRequestId, CancellationToken cancellationToken = default);

  // Approved and submitted requests overlapping a range, so a second request for days already booked can be
  // refused. Cancelled and rejected ones are excluded: they booked nothing.
  Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(
    Guid companyId, Guid employeeId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

  Task AddAsync(LeaveRequest request, CancellationToken cancellationToken = default);
}
