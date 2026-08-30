using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Approval;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Tests.Leave;

// ==================================================================================================
// THE ROOT-PATH SELF-APPROVAL BAR, END TO END THROUGH THE REAL ROUTER (BR-ATT-0007, T-084).
// ==================================================================================================
//
// ---- WHY THESE ARE NOT DOMAIN TESTS.
//
// `LeaveDomainTests` asserts the aggregate refuses when it is TOLD the acting employee is the requester.
// **That proves nothing about whether anybody tells it.** The aggregate cannot tell *the user was
// unresolvable* from *the handler never asked* — both arrive as `null`, both approve, and the aggregate is
// behaving correctly in each case.
//
// **That is FP-006P's shape: a correct file and an absence in a second one.** So the assertion that the
// resolution HAPPENS has to live where the resolution happens.
//
// ---- AND THE ROUTER IS REAL HERE, DELIBERATELY.
//
// `LeaveApprovalRouter` is the branch that produces the situation, not a layer that prevents it. Its case 2
// is *"every manager in the chain is the requester (a one-department company run by its manager)"* — it
// skips the requester when choosing a chain approver, falls through, and returns the root fallback. **A
// stubbed router would let these tests pass against a routing change that stopped producing case 2 at
// all**, which is the scenario the whole bar exists for.
public sealed class LeaveApprovalHandlerTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Manager = Guid.NewGuid();
  private static readonly Guid Department = Guid.NewGuid();
  private const long ActingTenantUserId = 4242;

  // ================================================================================================
  // CASE 2, END TO END. THE SCENARIO THE ROUTER NAMES AND NOTHING TESTED.
  // ================================================================================================
  //
  // One department, run by its own manager, who requests leave. The chain contains exactly that person; the
  // router skips them, falls through to the root fallback, and the handler resolves the acting user to the
  // same employee. **Before T-084 this approved silently.**
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public async Task The_one_department_manager_cannot_approve_their_own_leave_at_root()
  {
    var request = SubmittedByTheManager();
    var world = new World(request, resolvesTo: Manager);

    var result = await world.Approve();

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.SelfApprovalBarred.Code, result.Error.Code);

    // The route really was the root fallback — otherwise this would be asserting the ORDINARY bar and the
    // hole would still be open.
    Assert.True(world.LastRoute!.UsedRootFallback);
    Assert.Equal(LeaveRequestStatus.Submitted, request.Status);
    Assert.False(world.Saved);
  }

  // THE CONTROL. A different root holder decides the same request and it goes through — so the bar is on
  // the identity, not on the root path.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public async Task A_different_root_holder_may_approve_the_same_request()
  {
    var request = SubmittedByTheManager();
    var world = new World(request, resolvesTo: Guid.NewGuid());

    var result = await world.Approve();

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
    Assert.True(world.LastRoute!.UsedRootFallback);
    Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    Assert.Null(request.ApproverEmployeeId);
  }

  // ---- THE UNMAPPED OPERATOR, THROUGH THE HANDLER RATHER THAN THE AGGREGATE.
  //
  // `ADR-030` Decision 5. A platform-support holder with no linked employee is a normal caller and cannot
  // be the requester. **If this ever fails, the bar has become a mapping requirement** and the root
  // fallback is broken for the operator it exists for.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public async Task An_operator_with_no_linked_employee_may_still_approve_at_root()
  {
    var request = SubmittedByTheManager();
    var world = new World(request, resolvesTo: null);

    var result = await world.Approve();

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
    Assert.Equal(LeaveRequestStatus.Approved, request.Status);
  }

  // ================================================================================================
  // THE ASSERTION THE AGGREGATE CANNOT MAKE: THE HANDLER ACTUALLY ASKS.
  // ================================================================================================
  //
  // A handler that never called the resolver would pass every test above except this one — the aggregate
  // would receive `null`, read it as "unresolvable", and approve. **This asserts the question was asked,
  // and asked about the acting user rather than about somebody else.**
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public async Task The_handler_resolves_the_acting_user_before_deciding_at_root()
  {
    var world = new World(SubmittedByTheManager(), resolvesTo: Guid.NewGuid());

    await world.Approve();

    Assert.Equal([ActingTenantUserId], world.Resolver.Asked);
  }

  // ---- AND REJECT TOOK THE IDENTICAL PATH, SO IT HAD THE IDENTICAL HOLE.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public async Task The_same_bar_applies_to_a_root_rejection()
  {
    var request = SubmittedByTheManager();
    var world = new World(request, resolvesTo: Manager);

    var result = await world.Reject();

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.SelfApprovalBarred.Code, result.Error.Code);
    Assert.Equal(LeaveRequestStatus.Submitted, request.Status);
  }

  private static LeaveRequest SubmittedByTheManager() =>
    LeaveRequest.Submit(
      Company, Manager, Guid.NewGuid(),
      new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 25), 3m).Value;

  // The whole composition, with the REAL router over a chain containing only the requester — which is
  // exactly the router's case 2.
  private sealed class World
  {
    private readonly ApproveLeaveRequestCommandHandler approve;
    private readonly RejectLeaveRequestCommandHandler reject;
    private readonly LeaveRequest request;
    private readonly RecordingUnitOfWork unitOfWork = new();
    private readonly RecordingRouter router;

    public World(LeaveRequest request, Guid? resolvesTo)
    {
      this.request = request;
      Resolver = new RecordingResolver(resolvesTo);

      var scope = new PermissiveScope();
      var requests = new SingleRequestRepository(request);
      var currentUser = new StubCurrentUser();
      var tenantUser = new StubCurrentTenantUser(ActingTenantUserId);

      // The requester IS the only approver in the chain — a one-department company run by its manager.
      router = new RecordingRouter(new LeaveApprovalRouter(
        new SelfOnlyChain(Manager, Department), scope));

      approve = new ApproveLeaveRequestCommandHandler(
        requests, new EmptyLeaveTypes(), new EmptyBalances(), router, scope,
        currentUser, tenantUser, Resolver, unitOfWork);

      reject = new RejectLeaveRequestCommandHandler(
        requests, router, scope, currentUser, tenantUser, Resolver, unitOfWork);
    }

    public RecordingResolver Resolver { get; }

    public ApprovalRoute? LastRoute => router.Last;

    public bool Saved => unitOfWork.Saves > 0;

    public Task<Result> Approve() =>
      approve.HandleAsync(new DecideLeaveRequestCommand(request.Id, "decided"));

    public Task<Result> Reject() =>
      reject.HandleAsync(new DecideLeaveRequestCommand(request.Id, "decided"));
  }

  // Records every user it was asked about, which is what makes "the handler actually asks" assertable.
  private sealed class RecordingResolver(Guid? resolvesTo) : IUserEmployeeResolver
  {
    public List<long> Asked { get; } = [];

    public Task<Guid?> ResolveEmployeeIdAsync(long tenantUserId, CancellationToken cancellationToken = default)
    {
      Asked.Add(tenantUserId);
      return Task.FromResult(resolvesTo);
    }
  }

  // Wraps the REAL router so the test can assert which branch was taken without replacing the logic.
  private sealed class RecordingRouter(ILeaveApprovalRouter inner) : ILeaveApprovalRouter
  {
    public ApprovalRoute? Last { get; private set; }

    public async Task<Result<ApprovalRoute>> ResolveApproverAsync(
      Guid companyId, Guid requesterEmployeeId, CancellationToken cancellationToken = default)
    {
      var route = await inner.ResolveApproverAsync(companyId, requesterEmployeeId, cancellationToken);
      if (route.IsSuccess)
      {
        Last = route.Value;
      }

      return route;
    }
  }

  private sealed class SelfOnlyChain(Guid employeeId, Guid departmentId) : IEmployeeApproverDirectory
  {
    public Task<IReadOnlyList<ApproverCandidate>> GetApproverChainAsync(
      Guid companyId, Guid employeeId2, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<ApproverCandidate>>([new(employeeId, departmentId, Depth: 0)]);
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

  private sealed class EmptyLeaveTypes : ILeaveTypeRepository
  {
    public Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveType?>(LeaveType.Create(Company, "ANN", "Annual", LeaveBehaviour.PaidWithoutBalance, false).Value);  // PaidWithoutBalance does NOT consume, so these tests exercise the BAR and never the balance path

    public Task<bool> CodeExistsAsync(Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(LeaveType leaveType, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class EmptyBalances : ILeaveBalanceRepository
  {
    public Task<LeaveBalance?> GetByIdAsync(Guid leaveBalanceId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveBalance?>(null);

    public Task<LeaveBalance?> GetForEmployeeAsync(
      Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveBalance?>(null);

    public Task AddAsync(LeaveBalance balance, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class StubCurrentUser : ICurrentUser
  {
    public string? UserId => "root-admin";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class StubCurrentTenantUser(long tenantUserId) : ICurrentTenantUser
  {
    public long? TenantUserId => tenantUserId;
  }

  // Permission checks are not what these tests are about; the bar is. A scope that refused would make every
  // assertion below pass for the wrong reason.
  private sealed class PermissiveScope : IAttendanceScopeResolver
  {
    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests decide; they do not read.");

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests decide; they do not read.");

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
