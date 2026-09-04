using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Tests.Leave;

// ==================================================================================================
// ⚠⚠⚠ A RETIRED LEAVE TYPE REFUSES NEW BOOKINGS — AND NOTHING HAD EVER CHECKED IT.
// ==================================================================================================
//
// `LeaveCommandHandlers.cs:313-316` refuses a submission against an inactive leave type. **Before this
// file, no test in the repository ever put an inactive leave type in front of that handler.**
//
// ---- ⚠⚠ THE DECLARATION SIDE WAS FULLY DRESSED. THE PATH HAD NEVER RUN.
//
// This is not a forgotten corner. Every cheap, declaration-shaped test around leave-type deactivation
// exists and passes:
//
//   `POST /api/attendance/leave-types/{id}/deactivate` is MAPPED
//   it is in the route inventory                          `AttendanceRouteInventoryTests:78`
//   it is malformed-id tested                             `AttendanceMalformedIdentifierTests:54`
//   its handler is registered in the API host             `AttendanceApiTestHost:210`
//   the suite's only `/activate` call is a 404 case       `AttendanceLeaveTypeBalanceEndpointTests:96`
//
// **So the route was inventoried, routed, registered, permission-checked and id-validated — and no leave
// type was ever successfully deactivated anywhere in the suite.** The declaration side is cheap and was
// bought; the one expensive question — what happens to a BOOKING afterwards — was asked by nothing.
//
// ---- HOW THE ABSENCE WAS ESTABLISHED, BECAUSE A NAME SEARCH COULD NOT HAVE DONE IT.
//
// `LeaveType.Create` ALWAYS sets `IsActive = true` (`LeaveType.cs:119`) — the `bool` those factories pass
// is `isSensitive`, not activation, which is exactly the argument a reader skims past. `IsActive` is
// `{ get; private set; }` (`:163`) and its ONLY writer is `SetActivation` (`:221-230`). In `tests`,
// `SetActivation` was called in exactly one place — `LeaveDomainTests:37,41,43` — and no raw SQL anywhere
// touches a leave-type table. **Enumerating the writers, not the name, is what settled it.**
//
// ⚠ AND THAT ONE EXISTING TEST IS THE TRAP THIS FILE EXISTS TO AVOID:
// `A_leave_type_is_deactivated_never_deleted_and_reactivation_is_possible` asserts THE FLAG FLIPS —
// active, inactive, refused twice, active again — and never puts the object in front of a handler. **A
// state change is not a capability.**
public sealed class LeaveSubmissionActivationTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();

  // Far enough out that the request is always in the future, so no test here goes red on a calendar
  // boundary — the reason `LeaveCancellationHandlerTests` computes its dates the same way.
  private static DateOnly Future() => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

  // ⚠⚠ THE STATE IS PRODUCED BY THE PRODUCT'S OWN AND ONLY WRITER, NOT INJECTED.
  //
  // `SetActivation` is the sole path to `IsActive == false` outside EF materialization, so there is no
  // constructed row here whose fidelity would have to be argued — no columns to match, no events to
  // reproduce. A test that injected an inactive row would owe those arguments; this one owes none.
  //
  // ⚠⚠⚠ AND THE RESULT IS ASSERTED, WHICH IS NOT CEREMONY. `SetActivation` REFUSES A NO-OP
  // (`LeaveType.cs:223`): if this ever silently failed, the "inactive" type would still be ACTIVE, the
  // submission below would be refused for some other reason or not at all, and the test would be green
  // over an arrangement that never happened. **Assert the arrangement, not only the outcome.**
  private static LeaveType Retired()
  {
    var leaveType = LeaveType.Create(
      Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

    Assert.True(leaveType.IsActive, "a newly created leave type must be active, or this arranges nothing");
    Assert.True(
      leaveType.SetActivation(false).IsSuccess,
      "the deactivation was refused, so the type below is still ACTIVE and this test proves nothing");
    Assert.False(leaveType.IsActive);

    return leaveType;
  }

  // ⚠⚠ THE CLAUSE, NOT THE RULE. `AC-ATT-0022` (`acceptance-criteria.md:54`) has THREE:
  //
  //   1. a leave type is deactivated, never deleted
  //   2. a deactivated type CANNOT BE NAMED ON A NEW REQUEST        ← this test, and only this one
  //   3. existing requests that reference it remain intact
  //
  // Naming the rule alone would credit this with all three. It asserts the second and nothing else.
  //
  // ⚠ THIS TRAIT WAS `BR-ATT-0003` WHEN THE TEST LANDED AND THAT WAS FALSE — that rule is *the number of
  // days a leave request consumed is fixed at the moment of decision and does not change if the calendar is
  // later amended* (`business-rules.md:23`), about settled figures and calendar amendment, of which this
  // test asserts not one word. A trait naming a REAL rule the test does not satisfy is well-formed,
  // resolvable and wrong, and it inflates coverage in the direction of the gap.
  [Fact]
  [Trait("BusinessRule", "BR-ATT-0009")]
  [Trait("Criterion", "AC-ATT-0022")]
  public async Task A_submission_against_a_retired_leave_type_is_refused()
  {
    var leaveType = Retired();

    var result = await HandlerFor(leaveType).HandleAsync(new SubmitLeaveRequestCommand(
      Company, Employee, leaveType.Id, Future(), Future().AddDays(2)));

    Assert.True(result.IsFailure, "an inactive leave type must not accept a new booking");

    // ⚠⚠⚠ THE CODE, NOT THE FAILURE — AND THE PLANT MEASURED WHY THAT IS LOAD-BEARING.
    //
    // Deleting the guard from `LeaveCommandHandlers.cs:313-316` and running this test produced:
    //
    //   Assert.Equal() Failure: Strings differ
    //   Expected: "Attendance.LeaveTypeInactive"
    //   Actual:   "Attendance.LeaveEmployeeNotInCompany"
    //
    // **WITH THE GUARD GONE THE RESULT IS STILL A FAILURE.** Control ran on to the employment window and
    // was turned back there instead. So `Assert.True(result.IsFailure)` ALONE WOULD HAVE STAYED GREEN over
    // a deleted guard — the line above is not the test, this line is.
    Assert.Equal(LeaveErrors.LeaveTypeInactive.Code, result.Error.Code);
  }

  // ---- ⚠ THE CODE IS ASSERTED, NOT MERELY THE FAILURE, AND THIS IS THE REASON.
  //
  // The handler answers `LeaveTypeNotFound` when the type is missing OR belongs to another company
  // (`:307-311`), one branch above the activation check. A test asserting only `IsFailure` would pass
  // identically if the company ids drifted apart and the refusal came from the WRONG guard — the
  // arrangement would be broken and the test still green. Naming the code is what pins which refusal ran.
  // ⚠ NO `Criterion` TRAIT: this is a fixture control and satisfies no clause of `AC-ATT-0022`. It exists
  // so the refusal above cannot be coming from the construction. Tagging it would count the criterion twice
  // for one assertion.
  //
  // ---- ⚠⚠⚠ AND IT IS THE ONLY THING IN THE GATE THAT WITNESSES `AC-ATT-0043`, WHICH IS WHY IT STILL
  // CARRIES NO TRAIT.
  //
  // *"A leave request whose range falls outside the employee's employment window is refused, on the same
  // boundary reading as `AC-ATT-0007`."* **Measured: short-circuiting the leave employment-window check in
  // `LeaveCommandHandlers` to `Result.Success()` reddens THIS test and nothing else in seven suites.**
  //
  // ***SO THE REFUSAL IS WITNESSED AND ITS CRITERION IS UNANCHORED.*** The assertion is TRUE, it WITNESSES,
  // and it is INCIDENTAL: the code is asserted as a SENTINEL for "the activation guard released", by a test
  // whose subject is activation. **Citing `AC-ATT-0043` here would attach a criterion to an assertion that
  // exists to prove something else, and a legitimate change to the window's error code — a refactor this
  // test is designed to be brittle against for its OWN purpose — would then read as a criterion
  // regression.**
  //
  // ⚠ WHAT `AC-ATT-0043` ACTUALLY LACKS: a test that submits a request outside the window and asserts the
  // refusal ON ITS OWN TERMS, including which boundary — `RequestBeforeEmployment` versus
  // `RequestAfterTermination` — since the criterion inherits `AC-ATT-0007`'s inclusive reading and nothing
  // in the gate pins that the END date is what is checked against termination. **Recorded, not built:
  // the citation convention is with the owner.**
  [Fact]
  [Trait("BusinessRule", "BR-ATT-0009")]
  public async Task An_active_leave_type_gets_past_the_activation_guard()
  {
    // The SAME construction as `Retired`, minus the deactivation. If this answered `LeaveTypeInactive`
    // too, the refusal above would be telling us about the fixture rather than about activation.
    var leaveType = LeaveType.Create(
      Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

    var result = await HandlerFor(leaveType).HandleAsync(new SubmitLeaveRequestCommand(
      Company, Employee, leaveType.Id, Future(), Future().AddDays(2)));

    // ⚠⚠ IT DOES NOT SUCCEED, AND SAYING WHY MATTERS MORE THAN THE ASSERTION.
    //
    // Control advances to the employment window, whose roster this fixture does not stand up — so the
    // answer is a LATER refusal. **What is proved is that the activation guard RELEASED**: a different
    // and specific error code, from a step below the one under test.
    //
    // ⚠⚠⚠ THIS IS NOT THE CAPABILITY HALF AND MUST NOT BE READ AS IT. *A booking against a reactivated
    // type SUCCEEDS* needs a roster, a calendar, an overlap check, a submission lock and a unit of work
    // standing up together, which is a fixture this file does not build. Recorded as owed, deliberately,
    // rather than papered over with a not-this-error assertion dressed up as success.
    // ⚠ AND THIS TEST IS A FIXTURE CONTROL, NOT A SECOND GUARD DETECTOR — MEASURED, NOT ASSUMED. Under the
    // plant that deleted the guard it stayed GREEN, exactly as it should: it cannot see the guard's
    // presence and does not claim to. What it rules out is the refusal above coming from the CONSTRUCTION
    // rather than from the deactivation.
    Assert.True(result.IsFailure);
    Assert.NotEqual(LeaveErrors.LeaveTypeInactive.Code, result.Error.Code);
  }

  // ⚠⚠⚠ SIX OF THE EIGHT DEPENDENCIES THROW, AND THAT IS AN ASSERTION.
  //
  // The handler consults the scope resolver and the leave-type repository, in that order, before it
  // reaches the activation check. Everything else — requests, the submission lock, calendars, the roster,
  // the unit of work — must not be touched by a refusal, because a retired type is refused before any work
  // is done on its behalf.
  //
  // Returning empty stubs would let a later change move the guard BELOW the calendar or the overlap query
  // with nothing to notice. These throw instead, so such a change is a RED — one that names the dependency
  // it reached, which is more useful than an assertion counting calls.
  //
  // ---- ⚠⚠ WHY THE ORDER IS WORTH PINNING, BECAUSE AN ORDER PINNED WITHOUT ITS REASON IS JUST A TRIPWIRE.
  //
  // A retired leave type is a caller error known from one lookup. Refusing it before the submission lock is
  // taken means a bad request never serialises anything: `ILeaveSubmissionLock` is a per-employee,
  // transaction-owned lock, so admitting refusable work beneath it would make one employee's typo wait on
  // another's — and doing the calendar load and overlap query first would be I/O spent on a request that
  // cannot succeed. **Cheap refusals before expensive work is a design commitment, not an accident.**
  //
  // ⚠ SO IF SOMEONE LEGITIMATELY REORDERS THIS — a lock taken earlier for a reason nobody has needed yet —
  // THIS TEST REDDENS FOR A CORRECT CHANGE. That is the accepted cost, and the sentence above is what lets
  // the next person judge it rather than just silence the stub.
  //
  // ⚠⚠⚠ AND READ THE FAILURE TEXT, NOT THE COLOUR: a `NotSupportedException` from one of these stubs and a
  // failed `Assert.Equal` are BOTH red and mean different things. The exception means the guard MOVED
  // (execution got further than it should); the assertion means the guard is GONE. Measured on 2026-09-02:
  // deleting the guard produced the ASSERTION, not an exception — because the employment window sits
  // between it and the throwing stubs and answers first.
  private static SubmitLeaveRequestCommandHandler HandlerFor(LeaveType leaveType) => new(
    new UnreachableRequests(),
    new UnreachableLock(),
    new FixedLeaveTypes(leaveType),
    new UnreachableCalendars(),
    new UnreachableRoster(),
    new PermissiveScope(),
    new UnreachableUnitOfWork(),
    new FixedTenant());

  private sealed class FixedLeaveTypes(LeaveType leaveType) : ILeaveTypeRepository
  {
    public Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveType?>(leaveType);

    public Task<bool> CodeExistsAsync(
      Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(LeaveType type, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class PermissiveScope : IAttendanceScopeResolver
  {
    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests submit; they do not read.");

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests submit; they do not read.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());

    public Result RequirePermission(string permissionName) => Result.Success();

    public bool HasPermission(string permissionName) => true;
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId { get; } = Guid.NewGuid();
  }

  private sealed class UnreachableRequests : ILeaveRequestRepository
  {
    public Task<LeaveRequest?> GetByIdAsync(
      Guid leaveRequestId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not read an existing request.");

    public Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(
      Guid companyId, Guid employeeId, DateOnly startDate, DateOnly endDate,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A retired leave type is refused before any overlap query.");

    public Task AddAsync(LeaveRequest leaveRequest, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Nothing here may reach an insert.");
  }

  private sealed class UnreachableLock : ILeaveSubmissionLock
  {
    public Task<Result> AcquireAsync(
      Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not take the per-employee lock.");
  }

  private sealed class UnreachableCalendars : IWorkingCalendarRepository
  {
    public Task<WorkingCalendar?> GetByIdAsync(
      Guid workingCalendarId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task<WorkingCalendar?> GetForCompanyAsync(
      Guid companyId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A retired leave type is refused before the calendar is loaded.");

    public Task<bool> NameExistsAsync(
      Guid companyId, string normalizedName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task AddAsync(WorkingCalendar calendar, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task RemoveHolidayAsync(CalendarHoliday holiday, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");
  }

  // ⚠ THE ONE EXCEPTION, AND IT IS THE SECOND TEST'S WHOLE MECHANISM. The roster returns EMPTY rather than
  // throwing, because `An_active_leave_type_gets_past_the_activation_guard` needs control to REACH the
  // employment window and be turned back there. An empty roster is the honest shape of "this employee's
  // employment is not stood up", which is exactly the state this fixture is in.
  private sealed class UnreachableRoster : IEmployeeRoster
  {
    public Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
      Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<EmploymentRecord>>([]);
  }

  private sealed class UnreachableUnitOfWork : ITenantUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not save.");

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not open a transaction.");
  }
}
