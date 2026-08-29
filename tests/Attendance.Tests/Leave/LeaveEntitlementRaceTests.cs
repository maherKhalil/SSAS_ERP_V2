using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.SharedKernel;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Attendance.Tests.Leave;

// ==================================================================================================
// THE LOSER OF THE ENTITLEMENT RACE IS TOLD IT CAN RETRY (T-171).
// ==================================================================================================
//
// `SetLeaveEntitlementCommandHandler` reads with `GetForEmployeeAsync` and writes if it finds nothing, so
// two callers can both see null and both insert. `UX_AttendanceLeaveBalances_Employee_Type_Year` decides
// it at commit — and before this translation the loser received `Persistence.UniqueConstraint` unmapped,
// which is a 500.
//
// ---- ⚠ AND THE DUPLICATE ROW WAS NOT A REPORTING PROBLEM.
//
// `LeaveBalance.Consume` guards with `ConsumedQuantity + quantity > EntitlementQuantity` **against that
// row's own counter**, and the repository reads with `FirstOrDefaultAsync`. **Two rows meant the guard
// passed twice against two different counters — an employee could take double their entitlement, and the
// check that exists to prevent exactly that reported nothing.**
//
// ---- WHAT THIS TEST DOES AND DOES NOT PROVE.
//
// It proves the TRANSLATION: a unique-constraint failure from the save becomes
// `LeaveErrors.DuplicateBalance` rather than falling through to the generic write failure. **It does not
// prove the index exists** — that is the migration's job and an Integration test's, and the index is held
// pending a pre-flight against real tenant data (`scripts/preflight-leave-balance-duplicates.sql`).
public sealed class LeaveEntitlementRaceTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_lost_entitlement_race_is_named_rather_than_a_write_failure()
  {
    var leaveType = LeaveType.Create(Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

    var handler = new SetLeaveEntitlementCommandHandler(
      new NoBalances(),
      new SingleLeaveType(leaveType),
      new GrantingScope(),
      new FailingUnitOfWork(new Error(PersistenceErrorCodes.UniqueConstraint, "Unique index violated.")));

    var result = await handler.HandleAsync(
      new SetLeaveEntitlementCommand(Company, Employee, leaveType.Id, 2026, 20m));

    Assert.True(result.IsFailure);

    // The SPECIFIC refusal. A generic write failure would also be a failure here, and would tell the caller
    // nothing about whether retrying could ever succeed.
    Assert.Equal(LeaveErrors.DuplicateBalance, result.Error);
  }

  // ---- THE CONTROL, AND WITHOUT IT THE TEST ABOVE PROVES ALMOST NOTHING.
  //
  // A handler that turned EVERY save failure into `DuplicateBalance` would pass the test above and be
  // badly wrong — a storage outage would be reported to an operator as a duplicate entitlement.
  [Fact]
  public async Task Any_other_save_failure_is_passed_through_unchanged()
  {
    var leaveType = LeaveType.Create(Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;
    var other = new Error(PersistenceErrorCodes.WriteFailure, "The write did not complete.");

    var handler = new SetLeaveEntitlementCommandHandler(
      new NoBalances(), new SingleLeaveType(leaveType), new GrantingScope(), new FailingUnitOfWork(other));

    var result = await handler.HandleAsync(
      new SetLeaveEntitlementCommand(Company, Employee, leaveType.Id, 2026, 20m));

    Assert.True(result.IsFailure);
    Assert.Equal(other, result.Error);
  }

  private sealed class NoBalances : ILeaveBalanceRepository
  {
    public Task<LeaveBalance?> GetByIdAsync(Guid leaveBalanceId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveBalance?>(null);

    public Task<LeaveBalance?> GetForEmployeeAsync(
      Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveBalance?>(null);

    public Task AddAsync(LeaveBalance balance, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class SingleLeaveType(LeaveType leaveType) : ILeaveTypeRepository
  {
    public Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveType?>(leaveType.Id == leaveTypeId ? leaveType : null);

    public Task<bool> CodeExistsAsync(
      Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(LeaveType leaveType, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class GrantingScope : IAttendanceScopeResolver
  {
    public Result RequirePermission(string permissionName) => Result.Success();

    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Setting an entitlement resolves no read scope.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());

    public bool HasPermission(string permissionName) => true;

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Setting an entitlement resolves no read scope.");
  }

  private sealed class FailingUnitOfWork(Error error) : ITenantUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Failure<int>(error));

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("This handler opens no transaction.");
  }
}
