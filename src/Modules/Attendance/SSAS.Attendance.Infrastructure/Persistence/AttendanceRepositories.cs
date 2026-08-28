using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Attendance.Infrastructure.Persistence;

// Every query goes through `ITenantDbContextAccessor`, which resolves the tenant's context and applies the
// tenant global filter. None of these methods names a `TenantId` for that reason — adding one would be a
// second source of truth for an invariant the context already enforces.
//
// **None of them applies a BRANCH predicate either, and that is not an omission.** These are the WRITE-side
// ports; the branch boundary is enforced twice elsewhere and neither place is here. On the write path the
// boundary stamps and authorizes `BranchId` during save; on the read path `AttendanceReadScope` carries the
// authorized branch set into `AttendanceReadService`. A branch filter in a repository used by a write
// handler would be a third opinion, and three is how they come to disagree.
internal sealed class WorkingCalendarRepository(ITenantDbContextAccessor contextAccessor) : IWorkingCalendarRepository
{
  public async Task<WorkingCalendar?> GetByIdAsync(
    Guid workingCalendarId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Holidays always included — see the port for why there is no overload without them: `IsWorkingDay` is
    // meaningless without the list, and a loader that omitted it would answer "yes, a working day" for
    // every holiday.
    return await context.Set<WorkingCalendar>()
      .Include(calendar => calendar.Holidays)
      .FirstOrDefaultAsync(calendar => calendar.Id == workingCalendarId, cancellationToken);
  }

  public async Task<WorkingCalendar?> GetForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The default calendar first, then any other, so a company that never marked one still gets a stable
    // answer rather than an arbitrary one. Ordered by name after `IsDefault` so the fallback is
    // deterministic — two calls must not return different calendars and therefore different day counts.
    return await context.Set<WorkingCalendar>()
      .Include(calendar => calendar.Holidays)
      .Where(calendar => calendar.CompanyId == companyId)
      .OrderByDescending(calendar => calendar.IsDefault)
      .ThenBy(calendar => calendar.NormalizedName)
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<bool> NameExistsAsync(
    Guid companyId, string normalizedName, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Compared on the NORMALIZED column, which is binary-collated, so the database decides what counts as
    // the same name rather than the caller's culture.
    return await context.Set<WorkingCalendar>()
      .AnyAsync(
        calendar => calendar.CompanyId == companyId && calendar.NormalizedName == normalizedName,
        cancellationToken);
  }

  public async Task AddAsync(WorkingCalendar calendar, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<WorkingCalendar>().AddAsync(calendar, cancellationToken);
  }

  // See the port: the platform overrides this module's configured cascade with `Restrict`, so a holiday
  // taken out of the collection is an orphan nothing deletes. Marked Deleted by the handler BEFORE the
  // aggregate removes it — afterwards, EF's navigation fixer has already tried to null a non-nullable key.
  public async Task RemoveHolidayAsync(CalendarHoliday holiday, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(holiday);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    context.Set<CalendarHoliday>().Remove(holiday);
  }
}

internal sealed class AttendancePeriodRepository(ITenantDbContextAccessor contextAccessor) : IAttendancePeriodRepository
{
  public async Task<AttendancePeriod?> GetByIdAsync(
    Guid attendancePeriodId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<AttendancePeriod>()
      .FirstOrDefaultAsync(period => period.Id == attendancePeriodId, cancellationToken);
  }

  // Whatever its status — see the port. Filtering to open periods here would make `PeriodOpen` and
  // `PeriodNotFound` indistinguishable to the summary contract, and the two have different remedies:
  // one is "wait for the close", the other is "somebody has to create the period".
  public async Task<AttendancePeriod?> GetCoveringAsync(
    Guid companyId, DateOnly onDate, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<AttendancePeriod>()
      .Where(period => period.CompanyId == companyId)
      .Where(period => period.StartDate <= onDate && period.EndDate >= onDate)
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<AttendancePeriod?> GetCurrentOpenAsync(
    Guid companyId, DateOnly asOf, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The open period covering today if there is one; otherwise the most recent open period. An adjustment
    // has to land SOMEWHERE open, and refusing because today happens to fall in a gap between periods would
    // block a legitimate correction for a calendar reason.
    return await context.Set<AttendancePeriod>()
      .Where(period => period.CompanyId == companyId)
      .Where(period => period.Status == AttendancePeriodStatus.Open)
      .OrderByDescending(period => period.StartDate <= asOf && period.EndDate >= asOf)
      .ThenByDescending(period => period.StartDate)
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<bool> OverlapsAsync(
    Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Standard interval overlap: existing starts on or before the new one ends, and ends on or after it
    // begins. Written this way round rather than as three negated cases, because the negated form is where
    // off-by-one errors hide.
    return await context.Set<AttendancePeriod>()
      .AnyAsync(
        period => period.CompanyId == companyId &&
          period.StartDate <= endDate &&
          period.EndDate >= startDate,
        cancellationToken);
  }

  public async Task AddAsync(AttendancePeriod period, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<AttendancePeriod>().AddAsync(period, cancellationToken);
  }
}

internal sealed class AttendanceRecordRepository(ITenantDbContextAccessor contextAccessor) : IAttendanceRecordRepository
{
  public async Task<AttendanceRecord?> GetByIdAsync(
    Guid attendanceRecordId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- AsNoTracking, AND THAT MATTERS MORE HERE THAN ANYWHERE ELSE IN THE MODULE.
    //
    // This load exists to READ an observation so an adjustment can be built from it. A TRACKED
    // `IAppendOnlyEntity` is one careless `SaveChanges` away from `PreventAppendOnlyMutation` throwing — and
    // that exception would surface as an attendance failure rather than as the guard doing its job.
    //
    // `IPayrollRunRepository` split into three loaders for the same reason. Here one loader suffices because
    // nothing ever needs a tracked record.
    return await context.Set<AttendanceRecord>()
      .AsNoTracking()
      .FirstOrDefaultAsync(record => record.Id == attendanceRecordId, cancellationToken);
  }

  public async Task<IReadOnlyList<AttendanceRecord>> GetForEmployeePeriodAsync(
    Guid attendancePeriodId, Guid employeeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Observations AND adjustments, because the truth for an employee-date is their SUM. Untracked for the
    // reason above.
    return await context.Set<AttendanceRecord>()
      .AsNoTracking()
      .Where(record => record.AttendancePeriodId == attendancePeriodId && record.EmployeeId == employeeId)
      .OrderBy(record => record.AttendanceDate)
      .ThenBy(record => record.Id)
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<AttendanceRecord>().AddAsync(record, cancellationToken);
  }
}

internal sealed class LeaveTypeRepository(ITenantDbContextAccessor contextAccessor) : ILeaveTypeRepository
{
  public async Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<LeaveType>()
      .FirstOrDefaultAsync(leaveType => leaveType.Id == leaveTypeId, cancellationToken);
  }

  public async Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<LeaveType>()
      .AnyAsync(
        leaveType => leaveType.CompanyId == companyId && leaveType.NormalizedCode == normalizedCode,
        cancellationToken);
  }

  public async Task AddAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<LeaveType>().AddAsync(leaveType, cancellationToken);
  }
}

internal sealed class LeaveBalanceRepository(ITenantDbContextAccessor contextAccessor) : ILeaveBalanceRepository
{
  public async Task<LeaveBalance?> GetByIdAsync(Guid leaveBalanceId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<LeaveBalance>()
      .FirstOrDefaultAsync(balance => balance.Id == leaveBalanceId, cancellationToken);
  }

  public async Task<LeaveBalance?> GetForEmployeeAsync(
    Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // TRACKED, deliberately — unlike the attendance reads above. Approval consumes this balance, so the
    // entity must be tracked for the consumption to persist. `LeaveBalance` is not append-only; amending an
    // entitlement in place is exactly what `OD-ATT-0006`'s administered model means.
    return await context.Set<LeaveBalance>()
      .FirstOrDefaultAsync(
        balance => balance.CompanyId == companyId &&
          balance.EmployeeId == employeeId &&
          balance.LeaveTypeId == leaveTypeId &&
          balance.PeriodYear == periodYear,
        cancellationToken);
  }

  public async Task AddAsync(LeaveBalance balance, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<LeaveBalance>().AddAsync(balance, cancellationToken);
  }
}

internal sealed class LeaveRequestRepository(ITenantDbContextAccessor contextAccessor) : ILeaveRequestRepository
{
  public async Task<LeaveRequest?> GetByIdAsync(Guid leaveRequestId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<LeaveRequest>()
      .FirstOrDefaultAsync(request => request.Id == leaveRequestId, cancellationToken);
  }

  public async Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(
    Guid companyId, Guid employeeId, DateOnly startDate, DateOnly endDate,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Only requests that actually booked days. Cancelled and rejected ones booked nothing, so counting them
    // would refuse a legitimate resubmission after a rejection — which is the ordinary next step, not an
    // error.
    return await context.Set<LeaveRequest>()
      .AsNoTracking()
      .Where(request => request.CompanyId == companyId && request.EmployeeId == employeeId)
      .Where(request =>
        request.Status == LeaveRequestStatus.Submitted || request.Status == LeaveRequestStatus.Approved)
      .Where(request => request.StartDate <= endDate && request.EndDate >= startDate)
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(LeaveRequest request, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<LeaveRequest>().AddAsync(request, cancellationToken);
  }
}
