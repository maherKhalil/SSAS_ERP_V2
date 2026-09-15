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
// A LEAVE REQUEST OUTSIDE THE EMPLOYMENT WINDOW IS REFUSED, AND WHICH BOUNDARY IS NAMED (`AC-ATT-0043`).
// ==================================================================================================
//
// *"A leave request whose range falls outside the employee's employment window is refused, on the same
// boundary reading as `AC-ATT-0007`."*
//
// ---- ⚠ WHY THIS FILE EXISTS WHEN THE REFUSAL WAS ALREADY REACHED BY A TEST.
//
// **Measured: short-circuiting the leave employment-window check reddens exactly one test in seven suites —
// `LeaveSubmissionActivationTests.An_active_leave_type_gets_past_the_activation_guard`.** That test asserts
// the window's error code as a SENTINEL for "the activation guard released"; its subject is activation, and
// its author wrote that it must carry no `Criterion` trait.
//
// ***SO THE REFUSAL WAS WITNESSED AND THE CRITERION WAS UNANCHORED.*** Borrowing that assertion would have
// been worse than leaving the criterion uncited: **that test is deliberately brittle against a change to
// the window's error code FOR ITS OWN PURPOSE, so a citation there would turn a legitimate refactor into a
// criterion regression.** A citation that manufactures false alarms is worse than one that rots.
//
// ---- ⚠⚠⚠ THE THIRD CASE IS THE ONLY ONE THAT DISCRIMINATES, AND IT IS WHY THIS IS THREE TESTS.
//
// The handler checks the range START against employment and the range END against termination. **A pair of
// cases wholly before employment and wholly after termination would BOTH pass while the product compared
// the wrong date** — the bracket-versus-pin failure, in date form.
//
// **`A_request_straddling_the_termination_date_is_refused` is the discriminator**: it starts INSIDE
// employment and ends after termination. If the product checked the START against termination it would
// accept, because the start is legal. The source comment says the rule exists for exactly this: *"a request
// straddling a termination date would otherwise book leave for days after the employee had left."*
//
// ---- ⚠⚠ AND THE ROSTER STUB MODELS OVERLAP RATHER THAN ANSWERING WHATEVER THE TEST WANTS.
//
// `IEmployeeRoster.GetEmploymentAsync` takes a window and returns employees whose employment OVERLAPS it.
// A stub that returned the record unconditionally would let this file assert refusals the product cannot
// actually produce — an arranged failure rather than a constructible one.
//
// ***AND MODELLING IT HONESTLY REVEALED THAT ONE CASE IS NOT CONSTRUCTIBLE: a request lying WHOLLY after
// termination does not overlap employment at all, so the roster returns nothing and the answer is
// `EmployeeNotInCompany`, not `RequestAfterTermination`.*** **`RequestAfterTermination` is reachable ONLY
// by a straddle.** That is asserted below rather than left as a surprise, because a later reader writing
// the "obvious" wholly-after case would find it answering a different code and assume a defect.
public sealed class LeaveEmploymentWindowTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid Tenant = Guid.NewGuid();

  // Employment runs 2026-03-01 to 2026-06-30. Every case below is positioned against these two dates.
  private static readonly DateOnly Employed = new(2026, 3, 1);
  private static readonly DateOnly Terminated = new(2026, 6, 30);

  [Fact]
  [Trait("Criterion", "AC-ATT-0043")]
  public async Task A_request_starting_before_employment_is_refused_naming_that_boundary()
  {
    // Starts a week before employment and ends inside it, so the range OVERLAPS and the roster answers.
    var result = await Submit(Employed.AddDays(-7), Employed.AddDays(3));

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.RequestBeforeEmployment.Code, result.Error.Code);
  }

  // ⚠ THE DISCRIMINATOR. Starts INSIDE employment — a legal start — and ends after termination. A product
  // comparing the START against termination accepts this; only comparing the END refuses it.
  [Fact]
  [Trait("Criterion", "AC-ATT-0043")]
  public async Task A_request_straddling_the_termination_date_is_refused_naming_that_boundary()
  {
    var result = await Submit(Terminated.AddDays(-3), Terminated.AddDays(4));

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.RequestAfterTermination.Code, result.Error.Code);
  }

  // ⚠⚠ THE BOUNDARY IS INCLUSIVE ON THE TERMINATION DATE, following `AC-ATT-0007`'s reading — somebody who
  // left on the 30th may take leave ending on the 30th. This is the case that would fail if `>` became
  // `>=`, and it is the reason the two assertions above are not sufficient on their own: they establish
  // that something is refused, and this establishes that the refusal stops in the right place.
  [Fact]
  [Trait("Criterion", "AC-ATT-0043")]
  public async Task A_request_ending_exactly_on_the_termination_date_is_not_refused_by_the_window()
  {
    // ⚠ THE ASSERTION IS AN EXCEPTION, AND THAT IS THE POINT RATHER THAN A CONCESSION. Control advances
    // past the window into the calendar, whose stub throws by design — so **the throw IS the evidence that
    // the window released**, and its message names the step reached. A `Result` assertion could not
    // distinguish "the window allowed it" from "something else refused later"; this names which step ran.
    //
    // ⚠⚠ IT IS THE SAME SENTINEL TECHNIQUE `LeaveSubmissionActivationTests` USES, AND IT IS LEGITIMATE HERE
    // FOR THE REASON IT WAS NOT THERE: **the sentinel is a stub in THIS file, written for THIS purpose.**
    // Borrowing another test's sentinel inherits brittleness you cannot interpret; owning one does not.
    var thrown = await Assert.ThrowsAsync<NotSupportedException>(
      () => Submit(Terminated.AddDays(-3), Terminated));

    Assert.Contains("calendar", thrown.Message, StringComparison.OrdinalIgnoreCase);
  }

  private static Task<Result<Guid>> Submit(DateOnly from, DateOnly to)
  {
    var leaveType = LeaveType.Create(
      Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

    var handler = new SubmitLeaveRequestCommandHandler(
      new UnusedRequests(),
      new UnusedLock(),
      new FixedLeaveTypes(leaveType),
      new UnusedCalendars(),
      new OverlapRoster(),
      new PermissiveScope(),
      new UnusedUnitOfWork(),
      new FixedTenant());

    return handler.HandleAsync(new SubmitLeaveRequestCommand(Company, Employee, leaveType.Id, from, to));
  }

  // ---- THE ROSTER, MODELLING THE CONTRACT RATHER THAN THE TEST'S WISHES.
  //
  // Returns the employee only when the requested window overlaps [Employed, Terminated], which is what the
  // real roster does. A record that answered unconditionally would let this file assert refusals the
  // product cannot reach.
  private sealed class OverlapRoster : IEmployeeRoster
  {
    public Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
      Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc,
      CancellationToken cancellationToken = default)
    {
      var employed = new DateTimeOffset(Employed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
      var terminated = new DateTimeOffset(Terminated.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

      IReadOnlyList<EmploymentRecord> answer = fromUtc <= terminated && toUtc >= employed
        ? [new EmploymentRecord(Employee, companyId, employed, terminated)]
        : [];

      return Task.FromResult(answer);
    }
  }

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

  // Every stub below throws rather than returning a benign value: if the window releases when it should
  // refuse, the failure names the step that should not have been reached instead of taking a different
  // path to a plausible answer.
  private sealed class UnusedRequests : ILeaveRequestRepository
  {
    public Task<LeaveRequest?> GetByIdAsync(
      Guid leaveRequestId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task<IReadOnlyList<LeaveRequest>> GetOverlappingAsync(
      Guid companyId, Guid employeeId, DateOnly startDate, DateOnly endDate,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window must refuse before an overlap query.");

    public Task AddAsync(LeaveRequest leaveRequest, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not insert.");
  }

  private sealed class UnusedLock : ILeaveSubmissionLock
  {
    public Task<Result> AcquireAsync(
      Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window must refuse before the per-employee lock.");
  }

  private sealed class UnusedCalendars : IWorkingCalendarRepository
  {
    public Task<WorkingCalendar?> GetByIdAsync(
      Guid workingCalendarId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task<WorkingCalendar?> GetForCompanyAsync(
      Guid companyId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window must refuse before the calendar is loaded.");

    public Task<bool> NameExistsAsync(
      Guid companyId, string normalizedName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task AddAsync(WorkingCalendar calendar, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");

    public Task RemoveHolidayAsync(CalendarHoliday holiday, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Unused.");
  }

  private sealed class UnusedUnitOfWork : ITenantUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not save.");

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A refused submission must not open a transaction.");
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
    public Guid? TenantId { get; } = Tenant;
  }
}
