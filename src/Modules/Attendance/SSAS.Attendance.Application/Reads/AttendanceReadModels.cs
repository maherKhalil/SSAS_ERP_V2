using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Application.Reads;

// ATTENDANCE'S READ SHAPES. Projections, never entities: a read model is what a caller is allowed to see,
// and returning an aggregate would make that a question about serialization settings.

public sealed record WorkingCalendarView(
  Guid WorkingCalendarId,
  Guid CompanyId,
  string Name,
  IReadOnlyList<DayOfWeek> WeekendDays,
  bool IsDefault,
  IReadOnlyList<CalendarHolidayView> Holidays);

public sealed record CalendarHolidayView(DateOnly HolidayDate, string Name);

public sealed record AttendancePeriodView(
  Guid AttendancePeriodId,
  Guid CompanyId,
  string Name,
  DateOnly StartDate,
  DateOnly EndDate,
  string Status,
  DateTimeOffset? ClosedUtc,
  string? ClosedBy);

public sealed record AttendanceRecordView(
  Guid AttendanceRecordId,
  Guid CompanyId,
  Guid BranchId,
  Guid AttendancePeriodId,
  Guid EmployeeId,
  DateOnly AttendanceDate,
  string Kind,
  Guid? AdjustedRecordId,
  decimal WorkedQuantity,
  decimal OvertimeQuantity,
  string? OvertimeTier,
  decimal PaidAbsenceQuantity,
  decimal UnpaidAbsenceQuantity,
  string? Note);

public sealed record LeaveTypeView(
  Guid LeaveTypeId, Guid CompanyId, string Code, string Name, string Behaviour, bool IsSensitive, bool IsActive);

// ---- THE SENSITIVITY SPLIT MADE VISIBLE IN THE SHAPE (REQ-ATT-0025, OD-ATT-0013(3)).
//
// `LeaveTypeName` and `LeaveTypeCode` are NULLABLE, and null is not "missing data" — it means the caller does
// not hold `Attendance.Leave.ViewSensitive` for a type its company marked sensitive.
//
// Nullable fields rather than two record types, because a caller rendering a leave calendar wants ONE list
// with some entries redacted, not two lists it has to merge and re-sort. The redaction is per row because
// sensitivity is per type: annual leave stays visible in the same response where sick leave does not.
public sealed record LeaveRequestView(
  Guid LeaveRequestId,
  Guid CompanyId,
  Guid EmployeeId,
  Guid LeaveTypeId,
  string? LeaveTypeCode,
  string? LeaveTypeName,
  bool IsTypeRedacted,
  DateOnly StartDate,
  DateOnly EndDate,
  decimal WorkingDaysConsumed,
  string Status,
  string? DecidedBy,
  DateTimeOffset? DecidedUtc,
  Guid? ApproverEmployeeId,
  string? DecisionNote);

public sealed record LeaveBalanceView(
  Guid LeaveBalanceId,
  Guid CompanyId,
  Guid EmployeeId,
  Guid LeaveTypeId,
  int PeriodYear,
  decimal EntitlementQuantity,
  decimal ConsumedQuantity,
  decimal RemainingQuantity);

public interface IAttendanceReadService
{
  Task<Result<IReadOnlyList<WorkingCalendarView>>> GetCalendarsAsync(
    Guid? companyId, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<AttendancePeriodView>>> GetPeriodsAsync(
    Guid? companyId, CancellationToken cancellationToken = default);

  // ---- THE BRANCH-SCOPED READ (OD-ATT-0011).
  //
  // The only read in the module that applies a branch predicate, because `AttendanceRecord` is the only
  // branch-owned type. Contrast `IAttendanceSummary`, which is deliberately branch-blind.
  Task<Result<IReadOnlyList<AttendanceRecordView>>> GetRecordsAsync(
    Guid? companyId, Guid? employeeId, DateOnly? fromDate, DateOnly? toDate,
    CancellationToken cancellationToken = default);

  // ---- THE TWO SELF-SERVICE ENTRIES (FP-015, T-089).
  //
  // They take an `AttendanceReadScope` instead of resolving one, because the administrative entries resolve
  // ADMINISTRATIVE permissions (`Attendance.Records.View`, `Attendance.Leave.View`) that a self-service
  // caller must not need. The scope arrives from `IAttendanceSelfServiceScopeResolver`, built from the
  // caller's own employee record.
  //
  // **Handing a scope in is not a hole:** the factory is `internal` to this assembly, so a scope is still
  // proof that Attendance's own permission and placement resolution ran live. What a caller cannot do is
  // forge one.
  //
  // **`employeeId` is a method argument on both, never a member of any contract** — that is what keeps the
  // self routes free of an identifier a caller could change. Each shares its query with the administrative
  // entry, so the branch predicate cannot hold on one path and go missing on the other.
  Task<Result<IReadOnlyList<AttendanceRecordView>>> GetRecordsForEmployeeAsync(
    AttendanceReadScope readScope, Guid employeeId, DateOnly? fromDate, DateOnly? toDate,
    CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<LeaveRequestView>>> GetLeaveRequestsForEmployeeAsync(
    AttendanceReadScope readScope, Guid employeeId, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<LeaveTypeView>>> GetLeaveTypesAsync(
    Guid? companyId, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<LeaveRequestView>>> GetLeaveRequestsAsync(
    Guid? companyId, Guid? employeeId, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<LeaveBalanceView>>> GetLeaveBalancesAsync(
    Guid? companyId, Guid? employeeId, int? periodYear, CancellationToken cancellationToken = default);

  // Exposed because clients need the SAME answer the domain uses (`REQ-ATT-0003`). A client computing
  // working days itself would drift from the server the first time a holiday moved, and the drift would
  // show up as a leave request consuming a different number of days than the preview said it would.
  Task<Result<int>> GetWorkingDaysAsync(
    Guid companyId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
