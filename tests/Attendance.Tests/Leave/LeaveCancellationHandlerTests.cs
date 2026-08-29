using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Attendance.Tests.Leave;

// ==================================================================================================
// CANCELLING AN APPROVED REQUEST RETURNS THE DAYS, OR REFUSES (T-189).
// ==================================================================================================
//
// The release used to sit behind two `is not null` guards that fell through in silence. **A fall-through
// there does not fail — it reports SUCCESS while the employee's balance stays consumed**, which is the
// worst shape a defect can take: no error, no log, and a number that is quietly wrong from then on.
//
// ---- ⚠ NEITHER NULL IS REACHABLE TODAY. THAT IS THE POINT, NOT AN OBJECTION.
//
// The handler carries the five-link argument for why. These tests exist because every link is something a
// later change could break — a delete method, a `Behaviour` setter, an `IsActive` predicate added to a
// lookup — and each one would restore the silent loss. The tests are what make that visible instead.
//
// `The_days_actually_go_back` is the ANTI-VACUITY CONTROL: without it, deleting the release entirely would
// leave both refusals green, and a suite that cannot tell a working release from a missing one is not
// evidence about releases.
public sealed class LeaveCancellationHandlerTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid LeaveTypeId = Guid.NewGuid();
  private const decimal Days = 3m;

  [Fact]
  public async Task A_missing_leave_type_refuses_rather_than_cancelling_without_returning_the_days()
  {
    var world = new World(leaveType: null, balance: SomeBalance());

    var result = await world.Cancel();

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.LeaveTypeNotFound, result.Error);

    // Nothing persists: the aggregate was already cancelled in memory, so a save here would commit the
    // cancellation WITHOUT the release — precisely the outcome the refusal exists to prevent.
    Assert.False(world.Saved);
  }

  [Fact]
  public async Task A_missing_balance_refuses_rather_than_cancelling_without_returning_the_days()
  {
    var world = new World(leaveType: Metered(), balance: null);

    var result = await world.Cancel();

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.BalanceNotFound, result.Error);
    Assert.False(world.Saved);
  }

  [Fact]
  public async Task An_unmetered_type_cancels_without_touching_a_balance()
  {
    // `PaidWithoutBalance` consumed nothing at approval, so there is nothing to return — and the balance
    // lookup must not be reached at all, let alone refuse for finding nothing.
    var world = new World(leaveType: Unmetered(), balance: null);

    var result = await world.Cancel();

    Assert.True(result.IsSuccess);
    Assert.True(world.Saved);
  }

  [Fact]
  public async Task The_days_actually_go_back()
  {
    var balance = SomeBalance();
    Assert.Equal(Days, balance.ConsumedQuantity);

    var world = new World(leaveType: Metered(), balance: balance);

    var result = await world.Cancel();

    Assert.True(result.IsSuccess);
    Assert.Equal(0m, balance.ConsumedQuantity);
    Assert.True(world.Saved);
  }

  private static LeaveType Metered() =>
    LeaveType.Create(Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

  private static LeaveType Unmetered() =>
    LeaveType.Create(Company, "HAJ", "Hajj", LeaveBehaviour.PaidWithoutBalance, false).Value;

  // A balance that has ALREADY consumed the request's days, which is the only state an approved request
  // can have left behind.
  private static LeaveBalance SomeBalance()
  {
    var balance = LeaveBalance.Create(Company, Employee, LeaveTypeId, Future().Year, 20m).Value;
    balance.Consume(Days);
    return balance;
  }

  // Relative to today, so the request is always still in the future and `Cancel` is always permitted — a
  // fixed date would turn these green tests red on the day it passed.
  private static DateOnly Future() => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

  private static LeaveRequest Approved()
  {
    var request = LeaveRequest.Submit(
      Company, Employee, LeaveTypeId, Future(), Future().AddDays(2), Days).Value;

    request.Approve(Guid.NewGuid(), "the manager", DateTimeOffset.UtcNow, null);
    return request;
  }

  private sealed class World
  {
    private readonly CancelLeaveRequestCommandHandler handler;
    private readonly LeaveRequest request = Approved();
    private readonly RecordingUnitOfWork unitOfWork = new();

    public World(LeaveType? leaveType, LeaveBalance? balance)
    {
      handler = new CancelLeaveRequestCommandHandler(
        new SingleRequestRepository(request),
        new FixedLeaveTypes(leaveType),
        new FixedBalances(balance),
        new PermissiveScope(),
        unitOfWork);
    }

    public bool Saved => unitOfWork.Saves > 0;

    public Task<Result> Cancel() =>
      handler.HandleAsync(new CancelLeaveRequestCommand(request.Id));
  }

  private sealed class FixedLeaveTypes(LeaveType? leaveType) : ILeaveTypeRepository
  {
    public Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default) =>
      Task.FromResult(leaveType);

    public Task<bool> CodeExistsAsync(
      Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(LeaveType type, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class FixedBalances(LeaveBalance? balance) : ILeaveBalanceRepository
  {
    public Task<LeaveBalance?> GetByIdAsync(Guid leaveBalanceId, CancellationToken cancellationToken = default) =>
      Task.FromResult(balance);

    public Task<LeaveBalance?> GetForEmployeeAsync(
      Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(balance);

    public Task AddAsync(LeaveBalance entitlement, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class SingleRequestRepository(LeaveRequest request) : ILeaveRequestRepository
  {
    public Task<LeaveRequest?> GetByIdAsync(Guid leaveRequestId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveRequest?>(leaveRequestId == request.Id ? request : null);

    public Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(
      Guid companyId, Guid employeeId, DateOnly startDate, DateOnly endDate,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<LeaveRequest>>([]);

    public Task AddAsync(LeaveRequest leaveRequest, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class PermissiveScope : IAttendanceScopeResolver
  {
    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests cancel; they do not read.");

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests cancel; they do not read.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());

    public Result RequirePermission(string permissionName) => Result.Success();

    public bool HasPermission(string permissionName) => true;
  }

  private sealed class RecordingUnitOfWork : ITenantUnitOfWork
  {
    public int Saves { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      Saves++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests never open a transaction.");
  }
}
