using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Attendance.Infrastructure.Persistence;

// ATTENDANCE'S READS. Every one resolves a scope FIRST and filters by what the scope carries — never by a
// company or branch the caller named.
//
// A caller-supplied filter NARROWS an already-authorized set; it never widens one. That ordering is the
// whole guarantee, and it is why every method takes `Guid? companyId` as an optional narrowing rather than
// a required parameter: a required one invites a reader to assume it is doing the authorizing.
internal sealed class AttendanceReadService(
  ITenantDbContextAccessor contextAccessor,
  IAttendanceScopeResolver scope) : IAttendanceReadService
{
  public async Task<Result<IReadOnlyList<WorkingCalendarView>>> GetCalendarsAsync(
    Guid? companyId, CancellationToken cancellationToken = default)
  {
    // Company-only. A calendar is asserted NOT branch-owned, so a branch predicate would filter on a column
    // that does not exist.
    var resolved = await scope.ResolveCompanyOnlyAsync(
      AttendancePermissionNames.ViewCalendars, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<IReadOnlyList<WorkingCalendarView>>(resolved.Error);
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var calendars = await context.Set<WorkingCalendar>()
      .AsNoTracking()
      .Include(calendar => calendar.Holidays)
      .Where(calendar => calendar.TenantId == resolved.Value.TenantId)
      .Where(calendar => resolved.Value.CompanyIds.Contains(calendar.CompanyId))
      .Where(calendar => companyId == null || calendar.CompanyId == companyId)
      .OrderBy(calendar => calendar.NormalizedName)
      .ToListAsync(cancellationToken);

    // Projected after materialization: `WeekendPattern` is a value object whose day set cannot be
    // translated to SQL, and forcing it into the projection would either fail to translate or silently
    // evaluate client-side on the whole table.
    IReadOnlyList<WorkingCalendarView> views = calendars
      .Select(calendar => new WorkingCalendarView(
        calendar.Id,
        calendar.CompanyId,
        calendar.Name.Value,
        [.. calendar.Weekend.Days.OrderBy(day => (int)day)],
        calendar.IsDefault,
        [.. calendar.Holidays
          .OrderBy(holiday => holiday.HolidayDate)
          .Select(holiday => new CalendarHolidayView(holiday.HolidayDate, holiday.Name))]))
      .ToArray();

    return Result.Success(views);
  }

  public async Task<Result<IReadOnlyList<AttendancePeriodView>>> GetPeriodsAsync(
    Guid? companyId, CancellationToken cancellationToken = default)
  {
    var resolved = await scope.ResolveCompanyOnlyAsync(
      AttendancePermissionNames.ViewPeriods, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<IReadOnlyList<AttendancePeriodView>>(resolved.Error);
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    IReadOnlyList<AttendancePeriodView> views = await context.Set<AttendancePeriod>()
      .AsNoTracking()
      .Where(period => period.TenantId == resolved.Value.TenantId)
      .Where(period => resolved.Value.CompanyIds.Contains(period.CompanyId))
      .Where(period => companyId == null || period.CompanyId == companyId)
      .OrderByDescending(period => period.StartDate)
      .Select(period => new AttendancePeriodView(
        period.Id, period.CompanyId, period.Name.Value,
        period.StartDate, period.EndDate, period.Status.ToString(),
        period.ClosedUtc, period.ClosedBy))
      .ToListAsync(cancellationToken);

    return Result.Success(views);
  }

  // ================================================================================================
  // THE BRANCH-SCOPED READ — THE SUPERVISOR HALF OF OD-ATT-0011'S SPLIT.
  // ================================================================================================
  //
  // THREE predicates: tenant, company, **branch**. This is the only query in the product where branch
  // carries authorization meaning, and the branch set comes from `ITenantBranchAccessResolver` via the
  // scope — resolved LIVE at scope construction, active branches only, never from a token claim.
  //
  // Contrast `AttendanceSummaryService`, which applies NO branch predicate on purpose. The two files
  // together are the ruling; either one alone reads like a mistake.
  public async Task<Result<IReadOnlyList<AttendanceRecordView>>> GetRecordsAsync(
    Guid? companyId, Guid? employeeId, DateOnly? fromDate, DateOnly? toDate,
    CancellationToken cancellationToken = default)
  {
    var resolved = await scope.ResolveAsync(AttendancePermissionNames.ViewRecords, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<IReadOnlyList<AttendanceRecordView>>(resolved.Error);
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    IReadOnlyList<AttendanceRecordView> views = await context.Set<AttendanceRecord>()
      .AsNoTracking()
      .Where(record => record.TenantId == resolved.Value.TenantId)
      .Where(record => resolved.Value.CompanyIds.Contains(record.CompanyId))
      .Where(record => resolved.Value.BranchIds.Contains(record.BranchId))
      .Where(record => companyId == null || record.CompanyId == companyId)
      .Where(record => employeeId == null || record.EmployeeId == employeeId)
      .Where(record => fromDate == null || record.AttendanceDate >= fromDate)
      .Where(record => toDate == null || record.AttendanceDate <= toDate)
      .OrderByDescending(record => record.AttendanceDate)
      .ThenBy(record => record.Id)
      .Select(record => new AttendanceRecordView(
        record.Id, record.CompanyId, record.BranchId, record.AttendancePeriodId, record.EmployeeId,
        record.AttendanceDate, record.Kind.ToString(), record.AdjustedRecordId,
        record.WorkedQuantity, record.OvertimeQuantity, record.OvertimeTier,
        record.PaidAbsenceQuantity, record.UnpaidAbsenceQuantity, record.Note))
      .ToListAsync(cancellationToken);

    return Result.Success(views);
  }

  public async Task<Result<IReadOnlyList<LeaveTypeView>>> GetLeaveTypesAsync(
    Guid? companyId, CancellationToken cancellationToken = default)
  {
    var resolved = await scope.ResolveCompanyOnlyAsync(
      AttendancePermissionNames.ViewLeaveTypes, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<IReadOnlyList<LeaveTypeView>>(resolved.Error);
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The CATALOG is not redacted: knowing a company offers sick leave is not a fact about any person.
    // Redaction applies to a leave OCCURRENCE, where the type identifies an individual's medical absence —
    // see `GetLeaveRequestsAsync`.
    IReadOnlyList<LeaveTypeView> views = await context.Set<LeaveType>()
      .AsNoTracking()
      .Where(leaveType => leaveType.TenantId == resolved.Value.TenantId)
      .Where(leaveType => resolved.Value.CompanyIds.Contains(leaveType.CompanyId))
      .Where(leaveType => companyId == null || leaveType.CompanyId == companyId)
      .OrderBy(leaveType => leaveType.NormalizedCode)
      .Select(leaveType => new LeaveTypeView(
        leaveType.Id, leaveType.CompanyId, leaveType.Code.Value, leaveType.Name.Value,
        leaveType.Behaviour.ToString(), leaveType.IsSensitive, leaveType.IsActive))
      .ToListAsync(cancellationToken);

    return Result.Success(views);
  }

  // ================================================================================================
  // THE SENSITIVITY SPLIT, APPLIED IN THE PROJECTION (REQ-ATT-0025, OD-ATT-0013(3)).
  // ================================================================================================
  //
  // A caller holding `Attendance.Leave.View` sees THAT an employee is away. A caller additionally holding
  // `Attendance.Leave.ViewSensitive` sees WHICH TYPE.
  //
  // The split is applied **in the projection, per row**, not by refusing the whole request: annual leave
  // stays visible in the same response where sick leave is redacted, because the sensitivity is a property
  // of the type rather than of the request.
  //
  // `ViewPayslips` is the precedent — deliberately not folded into `ViewRuns`, because a run's existence and
  // totals are operational while the lines beneath them are an individual's pay.
  public async Task<Result<IReadOnlyList<LeaveRequestView>>> GetLeaveRequestsAsync(
    Guid? companyId, Guid? employeeId, CancellationToken cancellationToken = default)
  {
    var resolved = await scope.ResolveCompanyOnlyAsync(
      AttendancePermissionNames.ViewLeave, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<IReadOnlyList<LeaveRequestView>>(resolved.Error);
    }

    // Resolved ONCE, before the projection, so the same answer applies to every row. Asking per row would
    // make the redaction depend on evaluation order.
    var maySeeSensitive = scope.HasPermission(AttendancePermissionNames.ViewSensitiveLeave);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    IReadOnlyList<LeaveRequestView> views = await context.Set<LeaveRequest>()
      .AsNoTracking()
      .Where(request => request.TenantId == resolved.Value.TenantId)
      .Where(request => resolved.Value.CompanyIds.Contains(request.CompanyId))
      .Where(request => companyId == null || request.CompanyId == companyId)
      .Where(request => employeeId == null || request.EmployeeId == employeeId)
      .Join(
        context.Set<LeaveType>().AsNoTracking(),
        request => request.LeaveTypeId,
        leaveType => leaveType.Id,
        (request, leaveType) => new { request, leaveType })
      .OrderByDescending(row => row.request.StartDate)
      .Select(row => new LeaveRequestView(
        row.request.Id,
        row.request.CompanyId,
        row.request.EmployeeId,
        row.request.LeaveTypeId,

        // Redacted in the SQL projection rather than after materialization. The distinction matters: a
        // post-materialization filter would pull the sensitive value across the wire and out of the
        // database before discarding it, and it would appear in a query log either way.
        row.leaveType.IsSensitive && !maySeeSensitive ? null : row.leaveType.Code.Value,
        row.leaveType.IsSensitive && !maySeeSensitive ? null : row.leaveType.Name.Value,
        row.leaveType.IsSensitive && !maySeeSensitive,

        row.request.StartDate,
        row.request.EndDate,
        row.request.WorkingDaysConsumed,
        row.request.Status.ToString(),
        row.request.DecidedBy,
        row.request.DecidedUtc,
        row.request.ApproverEmployeeId,
        row.request.DecisionNote))
      .ToListAsync(cancellationToken);

    return Result.Success(views);
  }

  public async Task<Result<IReadOnlyList<LeaveBalanceView>>> GetLeaveBalancesAsync(
    Guid? companyId, Guid? employeeId, int? periodYear, CancellationToken cancellationToken = default)
  {
    var resolved = await scope.ResolveCompanyOnlyAsync(
      AttendancePermissionNames.ViewLeave, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<IReadOnlyList<LeaveBalanceView>>(resolved.Error);
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    IReadOnlyList<LeaveBalanceView> views = await context.Set<LeaveBalance>()
      .AsNoTracking()
      .Where(balance => balance.TenantId == resolved.Value.TenantId)
      .Where(balance => resolved.Value.CompanyIds.Contains(balance.CompanyId))
      .Where(balance => companyId == null || balance.CompanyId == companyId)
      .Where(balance => employeeId == null || balance.EmployeeId == employeeId)
      .Where(balance => periodYear == null || balance.PeriodYear == periodYear)
      .OrderByDescending(balance => balance.PeriodYear)
      .Select(balance => new LeaveBalanceView(
        balance.Id, balance.CompanyId, balance.EmployeeId, balance.LeaveTypeId, balance.PeriodYear,
        balance.EntitlementQuantity, balance.ConsumedQuantity,
        balance.EntitlementQuantity - balance.ConsumedQuantity))
      .ToListAsync(cancellationToken);

    return Result.Success(views);
  }

  public async Task<Result<int>> GetWorkingDaysAsync(
    Guid companyId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
  {
    var resolved = await scope.ResolveCompanyOnlyAsync(
      AttendancePermissionNames.ViewCalendars, cancellationToken);
    if (resolved.IsFailure)
    {
      return Result.Failure<int>(resolved.Error);
    }

    if (!resolved.Value.CompanyIds.Contains(companyId))
    {
      return Result.Failure<int>(AttendanceScopeErrors.CompanyScopeDenied);
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var calendar = await context.Set<WorkingCalendar>()
      .AsNoTracking()
      .Include(item => item.Holidays)
      .Where(item => item.TenantId == resolved.Value.TenantId)
      .Where(item => item.CompanyId == companyId)
      .OrderByDescending(item => item.IsDefault)
      .ThenBy(item => item.NormalizedName)
      .FirstOrDefaultAsync(cancellationToken);

    if (calendar is null)
    {
      return Result.Failure<int>(WorkingCalendarErrors.NoCalendarForCompany);
    }

    // The DOMAIN answers, not a reimplementation here. `WorkingCalendar.WorkingDaysBetween` is what
    // submission uses to freeze a request's consumed days, and a second implementation on the read side
    // would eventually disagree with the one that decides what people are owed.
    return Result.Success(calendar.WorkingDaysBetween(fromDate, toDate));
  }
}
