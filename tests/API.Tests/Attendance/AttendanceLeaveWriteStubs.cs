using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Contracts.Employment;

namespace SSAS.API.Tests.Attendance;

// =================================================================================================
// THE WRITE-PATH DOUBLES FOR ATTENDANCE'S ADMINISTRATIVE SURFACE (T-197).
// =================================================================================================
//
// ---- ⚠ THESE EXIST BECAUSE THE HOST REGISTERED NO HANDLERS AT ALL, AND NOTHING SAID SO.
//
// `AttendanceApiTestHost` called `AddAttendanceModule()` — which registers ONE endpoint filter — and never
// `AddAttendanceInfrastructure()`, which is where every repository and every command handler lives. So the
// module's writes had no composition to reach and **not one HTTP request had ever been issued against leave
// requests, leave types, balances, calendars, periods or records.**
//
// The gap was invisible because the folder looked well tested: a route inventory listing all 25 routes, a
// permission sweep asserting each one requires a permission, a transport test reflecting over request
// records, and a self-service test exercising `/me` only. **None of them issues a request.** A permission
// sweep that never calls a route is the route-inventory failure one level up.
//
// ---- WHAT THESE TESTS CAN AND CANNOT PROVE, STATED RATHER THAN LEFT TO A READER.
//
// The handlers are already well covered in `Attendance.Tests` against these same kinds of doubles. **What
// is unproven, and what only an HTTP test can reach, is the WIRING**: that the route exists at the
// documented path and method, that the body binds to the request record, that the company header is
// established, that a domain refusal becomes the right status and problem code rather than a 500.
//
// So these stubs are deliberately DUMB. They answer what they are told to answer, and the assertions are
// about what came back over the wire. A test here that asserted business behaviour would be proving the
// stub — the T-187 mistake, in a module that has not made it yet.
public sealed class StubLeaveRequests : ILeaveRequestRepository
{
  public LeaveRequest? Existing { get; set; }

  public List<LeaveRequest> Overlapping { get; } = [];

  public List<LeaveRequest> Added { get; } = [];

  public Task<LeaveRequest?> GetByIdAsync(Guid leaveRequestId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(
    Guid companyId, Guid employeeId, DateOnly startDate, DateOnly endDate,
    CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<LeaveRequest>>(Overlapping);

  public Task AddAsync(LeaveRequest request, CancellationToken cancellationToken = default)
  {
    Added.Add(request);
    return Task.CompletedTask;
  }
}

public sealed class StubLeaveTypes : ILeaveTypeRepository
{
  // A metered type by default, because that is the path with the most wiring behind it: balance lookup,
  // consumption, and the release on cancellation.
  public LeaveType? Existing { get; set; } =
    LeaveType.Create(AttendanceApiTestHost.CompanyA, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

  public bool CodeTaken { get; set; }

  public Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task AddAsync(LeaveType leaveType, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class StubLeaveBalances : ILeaveBalanceRepository
{
  public LeaveBalance? Existing { get; set; } = LeaveBalance.Create(
    AttendanceApiTestHost.CompanyA, AttendanceApiTestHost.EmployeeId,
    Guid.Parse("66666666-6666-6666-6666-666666666666"), 2026, 30m).Value;

  public Task<LeaveBalance?> GetByIdAsync(Guid leaveBalanceId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<LeaveBalance?> GetForEmployeeAsync(
    Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task AddAsync(LeaveBalance balance, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class StubWorkingCalendars : IWorkingCalendarRepository
{
  // ⚠ SEEDED, for the same reason the roster is. A null calendar refuses every leave submission with a 422
  // `attendance.calendar_missing` — a deliberate and correct refusal, and not the one under test here.
  // Fri/Sat weekend, which is the Saudi working week this product is built for.
  public WorkingCalendar? Existing { get; set; } = WorkingCalendar.Create(
    AttendanceApiTestHost.CompanyA, "Standard",
    [DayOfWeek.Friday, DayOfWeek.Saturday], true).Value;

  public bool NameTaken { get; set; }

  public Task<WorkingCalendar?> GetByIdAsync(
    Guid workingCalendarId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<WorkingCalendar?> GetForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<bool> NameExistsAsync(
    Guid companyId, string normalizedName, CancellationToken cancellationToken = default) =>
    Task.FromResult(NameTaken);

  public Task AddAsync(WorkingCalendar calendar, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;

  public Task RemoveHolidayAsync(CalendarHoliday holiday, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;
}

// ⚠ GRANTS BY DEFAULT, AND THAT IS A LIMIT ON WHAT THESE TESTS PROVE, NOT A CONVENIENCE.
//
// The real lock refuses without an open transaction and refuses a second concurrent submitter, and neither
// is reachable over HTTP against a stub. That behaviour is proven where it can be —
// `AttendanceOverlapChainSqlServerTests`, on two real connections. **What is proven HERE is only that a
// refusal from this seam becomes the right status**, which is why `Failure` is settable.
// The period store (T-199, slice 2). `Overlapping` defaults FALSE and `Existing` defaults NULL, and both
// defaults are answers rather than absences — see the roster note below, which cost two failing runs to
// learn. A test that needs a period sets one.
public sealed class StubAttendancePeriods : IAttendancePeriodRepository
{
  public AttendancePeriod? Existing { get; set; }

  public bool Overlapping { get; set; }

  public List<AttendancePeriod> Added { get; } = [];

  public Task<AttendancePeriod?> GetByIdAsync(
    Guid attendancePeriodId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<AttendancePeriod?> GetCoveringAsync(
    Guid companyId, DateOnly onDate, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<AttendancePeriod?> GetCurrentOpenAsync(
    Guid companyId, DateOnly asOf, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<bool> OverlapsAsync(
    Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
    Task.FromResult(Overlapping);

  public Task AddAsync(AttendancePeriod period, CancellationToken cancellationToken = default)
  {
    Added.Add(period);
    return Task.CompletedTask;
  }
}

// The record store (T-208, slice 4). `Existing` defaults NULL and `ForPeriod` defaults EMPTY, and both are
// ANSWERS rather than absences -- see the roster note below, which cost two failing runs to learn.
public sealed class StubAttendanceRecords : IAttendanceRecordRepository
{
  public AttendanceRecord? Existing { get; set; }

  public List<AttendanceRecord> ForPeriod { get; } = [];

  public List<AttendanceRecord> Added { get; } = [];

  public Task<AttendanceRecord?> GetByIdAsync(
    Guid attendanceRecordId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing);

  public Task<IReadOnlyList<AttendanceRecord>> GetForEmployeePeriodAsync(
    Guid attendancePeriodId, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<AttendanceRecord>>(ForPeriod);

  public Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
  {
    Added.Add(record);
    return Task.CompletedTask;
  }
}

public sealed class StubLeaveSubmissionLock : ILeaveSubmissionLock
{
  public Error? Failure { get; set; }

  public Task<Result> AcquireAsync(
    Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Failure is null ? Result.Success() : Result.Failure(Failure));
}

public sealed class StubEmployeeRoster : IEmployeeRoster
{
  // ⚠ SEEDED EMPLOYED, because an EMPTY roster refuses every submission with `attendance.employment_window`
  // before the request reaches anything else. The first run of these tests failed that way and it was the
  // stub, not the module — a double that answers "nothing" is not neutral, it is a specific answer.
  public List<EmploymentRecord> Employment { get; } =
  [
    new(AttendanceApiTestHost.EmployeeId, AttendanceApiTestHost.CompanyA,
      new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), null),
  ];

  public Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
    Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc,
    CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<EmploymentRecord>>(Employment);
}

public sealed class StubApproverDirectory : IEmployeeApproverDirectory
{
  public List<ApproverCandidate> Chain { get; } = [];

  public Task<IReadOnlyList<ApproverCandidate>> GetApproverChainAsync(
    Guid companyId, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<ApproverCandidate>>(Chain);
}

public sealed class StubBranchAccess : ITenantBranchAccessResolver
{
  public Task<Result<IReadOnlyList<BranchAccessSummary>>> GetPermittedBranchesAsync(
    Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<BranchAccessSummary>>([]));

  public Task<Result> AuthorizeBranchAsync(
    Guid tenantId, long tenantUserId, Guid branchId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success());
}

// Records saves rather than performing them, and can be told to fail — which is how a persistence refusal's
// mapping to a status is reached over the wire without a database.
public sealed class StubAttendanceUnitOfWork : ITenantUnitOfWork
{
  public int Saves { get; private set; }

  public Error? Failure { get; set; }

  public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    Saves++;
    return Task.FromResult(Failure is null ? Result.Success(1) : Result.Failure<int>(Failure));
  }

  public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
    Task.FromResult<ITransaction>(new NoOpTransaction());

  public void Reset()
  {
    Saves = 0;
    Failure = null;
  }

  private sealed class NoOpTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
