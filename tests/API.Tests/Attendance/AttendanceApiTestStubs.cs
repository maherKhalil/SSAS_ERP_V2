using SSAS.Attendance.Application.Reads;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.HR.Contracts.Employment;

namespace SSAS.API.Tests.Attendance;

// ==================================================================================================
// THE ATTENDANCE READ STUB — IT RECORDS THE SCOPE IT WAS HANDED, WHICH IS WHAT THE TESTS ASSERT ON.
// ==================================================================================================
//
// A self-service read's authorization is not visible in its RESULT. The handler returns whatever the read
// service returns, so a stub that only returned rows would let a test pass against a handler that resolved
// nobody, filtered on nothing, and happened to be given the right data.
//
// **What must be asserted is the SCOPE and the EMPLOYEE the handler passed in** — those carry the whole
// guarantee — so this stub captures both and the tests read them back.
//
// The administrative methods return empty successes rather than throwing: the host maps the module's whole
// surface, so an accidental call should fail on an ASSERTION about what was captured rather than on a stub
// exception that reads like an infrastructure fault.
public sealed class StubAttendanceReads : IAttendanceReadService
{
  public sealed record RecordsCall(
    AttendanceReadScope Scope, Guid EmployeeId, DateOnly? FromDate, DateOnly? ToDate);

  public sealed record LeaveCall(AttendanceReadScope Scope, Guid EmployeeId);

  public List<RecordsCall> RecordsForEmployee { get; } = [];

  public List<LeaveCall> LeaveForEmployee { get; } = [];

  public List<AttendanceRecordView> Records { get; set; } = [];

  public List<LeaveRequestView> LeaveRequests { get; set; } = [];

  public Task<Result<IReadOnlyList<AttendanceRecordView>>> GetRecordsForEmployeeAsync(
    AttendanceReadScope readScope, Guid employeeId, DateOnly? fromDate, DateOnly? toDate,
    CancellationToken cancellationToken = default)
  {
    RecordsForEmployee.Add(new(readScope, employeeId, fromDate, toDate));
    return Task.FromResult(Result.Success<IReadOnlyList<AttendanceRecordView>>(Records));
  }

  public Task<Result<IReadOnlyList<LeaveRequestView>>> GetLeaveRequestsForEmployeeAsync(
    AttendanceReadScope readScope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    LeaveForEmployee.Add(new(readScope, employeeId));
    return Task.FromResult(Result.Success<IReadOnlyList<LeaveRequestView>>(LeaveRequests));
  }

  public Task<Result<IReadOnlyList<WorkingCalendarView>>> GetCalendarsAsync(
    Guid? companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<WorkingCalendarView>>([]));

  public Task<Result<IReadOnlyList<AttendancePeriodView>>> GetPeriodsAsync(
    Guid? companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<AttendancePeriodView>>([]));

  public Task<Result<IReadOnlyList<AttendanceRecordView>>> GetRecordsAsync(
    Guid? companyId, Guid? employeeId, DateOnly? fromDate, DateOnly? toDate,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<AttendanceRecordView>>([]));

  public Task<Result<IReadOnlyList<LeaveTypeView>>> GetLeaveTypesAsync(
    Guid? companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<LeaveTypeView>>([]));

  public Task<Result<IReadOnlyList<LeaveRequestView>>> GetLeaveRequestsAsync(
    Guid? companyId, Guid? employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<LeaveRequestView>>([]));

  public Task<Result<IReadOnlyList<LeaveBalanceView>>> GetLeaveBalancesAsync(
    Guid? companyId, Guid? employeeId, int? periodYear, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<LeaveBalanceView>>([]));

  // Settable so a test can tell PASS-THROUGH from a handler computing its own answer. Returning a constant
  // 0 made both look identical, and the route existed for two months with nothing calling it.
  public int WorkingDays { get; set; }

  public Guid? WorkingDaysCompanyId { get; private set; }

  public (DateOnly From, DateOnly To)? WorkingDaysRange { get; private set; }

  public Task<Result<int>> GetWorkingDaysAsync(
    Guid companyId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
  {
    WorkingDaysCompanyId = companyId;
    WorkingDaysRange = (fromDate, toDate);
    return Task.FromResult(Result.Success(WorkingDays));
  }

  public void Reset()
  {
    WorkingDays = 0;
    WorkingDaysCompanyId = null;
    WorkingDaysRange = null;
    RecordsForEmployee.Clear();
    LeaveForEmployee.Clear();
    Records = [];
    LeaveRequests = [];
  }
}

// FP-015's two doors out of the module, on one object.
//
// `GetPlacementAsync` answers only for the employee the link actually names. Setting `LinkedEmployee` to an
// employee the placement does not know is how a test reaches the DANGLING LINK case — a link outliving the
// employee it names, which `ADR-030` Decision 4 makes reachable because no cross-database foreign key can
// prevent it.
public sealed class StubAttendanceSelfServiceDirectory : IUserEmployeeResolver, IEmployeePlacementDirectory
{
  public Guid? LinkedEmployee { get; set; } = AttendanceApiTestHost.EmployeeId;

  public EmployeePlacement? EmployeePlacement { get; set; } =
    new(AttendanceApiTestHost.CompanyA, AttendanceApiTestHost.BranchId);

  public List<long> AskedForUser { get; } = [];

  public List<Guid> AskedForPlacement { get; } = [];

  public Task<Guid?> ResolveEmployeeIdAsync(long tenantUserId, CancellationToken cancellationToken = default)
  {
    AskedForUser.Add(tenantUserId);
    return Task.FromResult(LinkedEmployee);
  }

  public Task<EmployeePlacement?> GetPlacementAsync(
    Guid employeeId, CancellationToken cancellationToken = default)
  {
    AskedForPlacement.Add(employeeId);
    return Task.FromResult(employeeId == LinkedEmployee ? EmployeePlacement : null);
  }

  public void Reset()
  {
    LinkedEmployee = AttendanceApiTestHost.EmployeeId;
    EmployeePlacement = new(AttendanceApiTestHost.CompanyA, AttendanceApiTestHost.BranchId);
    AskedForUser.Clear();
    AskedForPlacement.Clear();
  }
}
