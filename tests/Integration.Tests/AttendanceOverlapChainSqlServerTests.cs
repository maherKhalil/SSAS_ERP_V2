using SSAS.HR.Contracts.Employment;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Application.Leave;
using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Application.Periods;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Periods;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// ATTENDANCE'S TWO UNBACKED RANGE-OVERLAP INVARIANTS, AGAINST A REAL DATABASE (T-146).
// ==================================================================================================
//
// **Two invariants, both guarding RANGE OVERLAP, both behind an index on exactly the range columns that
// is deliberately NOT unique, both enforced by application code alone, and both feeding a payslip:**
//
//   CreateAttendancePeriodCommandHandler   OverlapsAsync        index :183, not unique
//   SubmitLeaveRequestCommandHandler       GetOverlappingAsync  index :416, not unique
//
// **Named for the shape rather than for the period half** — the file began as period resolution and
// gained the leave half when a rescan found it, and a name that says "period" while the file covers two
// invariants is the same defect as a test called `..._all_seven_payroll_tables` that counts eight.
//
// **T-143 measured that NONE of Attendance's seventeen command handlers had ever run against SQL Server** —
// the only module at 0%. This is the first, and it is the one payroll's money depends on.
//
// ---- WHY THIS SEAM AND NOT THE RECORD ITSELF.
//
// `PayrollChainSqlServerTests` already persists attendance — **but it builds the rows AND the period by
// hand**, straight through the aggregate and the context:
//
// ```
// var period = AttendancePeriod.Create(...);   context.Set<AttendancePeriod>().Add(period);
// var worked = AttendanceRecord.Observe(...);  // no handler anywhere in the path
// ```
//
// **So the domain factory has met a database and the handler path has not** — and the handler is where the
// period a date belongs to is DECIDED, by `IAttendancePeriodRepository.GetCoveringAsync`.
//
// **Payroll reads attendance BY PERIOD.** A date resolving to the wrong period puts a day's work on the
// wrong payslip, and the one test that persists attendance assigns the period by hand, **so it could never
// notice.**
//
// ---- ⚠ THE QUERY IS DETERMINISTIC ONLY BECAUSE OF AN INVARIANT APPLICATION CODE ENFORCES ALONE.
//
// ```
// GetCoveringAsync   StartDate <= onDate && EndDate >= onDate   FirstOrDefaultAsync, NO OrderBy
// OverlapsAsync      StartDate <= endDate && EndDate >= startDate
// ```
//
// **Both are closed intervals, so the overlap guard refuses any period sharing even one day — which is
// exactly strong enough to make the unordered `FirstOrDefault` deterministic.** If two periods ever shared a
// date, that query would return **an arbitrary one**, and period assignment would be decided by the
// database's choice of row.
//
// **And the schema says, in as many words, that the handler is the only thing holding this up:**
//
// > *`AttendanceConfigurations.cs:180` — "**Not unique** — a period is identified by its range, and the
// > overlap check **in the handler** is what keeps the ranges disjoint. An index on the range supports both
// > that check and `GetCoveringAsync`."*
//
// **A non-unique index is a decision, not an oversight.** The product states where its safety lives — and
// until this file, that code had never been executed against the engine it protects.
//
// ---- THE TWO ASSERTIONS DO DIFFERENT WORK.
//
//   the refusal    proves the guard RUNS
//   the boundary   proves the guard is STRONG ENOUGH — that two closed predicates agreeing on paper
//                  actually yield one row against a real engine
//
// **The second is the one that matters.** Reading is what failed at FP-013, twice.
public sealed class AttendanceOverlapChainSqlServerTests
{
  // The fixture seeds September 2026 (1st–30th). A period starting ON the 30th shares exactly one day.
  [Fact]
  [Trait("Decision", "OD-ATT-0012")]
  public async Task A_period_sharing_a_single_day_with_an_existing_one_is_refused()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    // ⚠ SEEDING IS A CALL, NOT A SIDE EFFECT OF CreateAsync. The fixture creates the catalog; the
    // September period exists only because this line asks for it. Discovered by probing the context
    // rather than by reading `SeedRecordAsync`'s body — which is written as though it always runs.
    await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();

    var created = await HandlerFor(context).HandleAsync(new CreateAttendancePeriodCommand(
      fixture.CompanyA, "Overlaps by one day", new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31)));

    Assert.True(created.IsFailure);

    // The specific refusal, not merely "it failed" — an authorisation or validation failure would also be a
    // failure and would prove nothing about the overlap guard.
    Assert.Equal(AttendancePeriodErrors.OverlapsExistingPeriod, created.Error);
  }

  // ---- THE ONE THAT MATTERS. A LEGAL ADJACENT PERIOD, AND THE BOUNDARY STILL RESOLVES TO EXACTLY ONE.
  [Fact]
  [Trait("Decision", "OD-ATT-0012")]
  public async Task A_boundary_date_is_covered_by_exactly_one_period()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();

    // October starts the day after September ends — adjacent, not overlapping, so this must SUCCEED.
    var october = await HandlerFor(context).HandleAsync(new CreateAttendancePeriodCommand(
      fixture.CompanyA, "October 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

    Assert.True(october.IsSuccess, october.IsFailure ? october.Error.Code : null);

    // ---- ASSERTED ON THE ROWS, NOT ON THE REPOSITORY.
    //
    // `GetCoveringAsync` returns ONE period by construction — `FirstOrDefaultAsync` cannot return two. So
    // asking it whether exactly one matches would be asking a question it cannot answer. **The count of rows
    // satisfying its own predicate is what proves the guard held.**
    foreach (var boundary in new[] { new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1) })
    {
      var covering = await context.Set<AttendancePeriod>()
        .Where(period => period.CompanyId == fixture.CompanyA)
        .Where(period => period.StartDate <= boundary && period.EndDate >= boundary)
        .CountAsync();

      Assert.Equal(1, covering);
    }

    // And the repository agrees with the rows — the two boundaries land in different periods.
    var periods = new AttendancePeriodRepository(new SingleContext(context));
    var endOfSeptember = await periods.GetCoveringAsync(fixture.CompanyA, new DateOnly(2026, 9, 30));
    var startOfOctober = await periods.GetCoveringAsync(fixture.CompanyA, new DateOnly(2026, 10, 1));

    Assert.NotNull(endOfSeptember);
    Assert.NotNull(startOfOctober);
    Assert.NotEqual(endOfSeptember!.Id, startOfOctober!.Id);
  }

  // ================================================================================================
  // THE SECOND UNBACKED INVARIANT: OVERLAPPING LEAVE (T-146).
  // ================================================================================================
  //
  // **`SubmitLeaveRequestCommandHandler` guards against a second request for days already booked** —
  // `GetOverlappingAsync` at `LeaveCommandHandlers.cs:258`, refusing with `RequestOverlaps`.
  //
  // **And the index behind it is NOT unique:**
  //
  // ```
  // AttendanceConfigurations.cs:416
  //   builder.HasIndex(request => new { request.TenantId, request.EmployeeId, request.StartDate, request.EndDate });
  // ```
  //
  // **Exactly the range columns the guard protects, deliberately not constraining** — the same pattern as
  // periods, in a second place. **Nothing in the engine prevents two approved requests covering one day for
  // one employee**, and this handler had never run against a database.
  //
  // **It reaches money by the same route as a misresolved period, through a different door:** leave becomes
  // paid or unpaid absence, absence feeds `UnpaidAbsenceQuantity`, and that is a payslip line. **Double-booked
  // leave is double-counted absence.**
  //
  // ---- IT WAS ALMOST MISSED, AND THE REASON IS WORTH THE COMMENT.
  //
  // A first scan of each handler used a fixed line window and reported this one as having NO guard — the
  // call sits ~57 lines into the class. **A bounded window read as the whole unit.** The rescan that found it
  // matches braces instead. **The window was chosen, not defaulted, which is what made it invisible.**
  [Fact]
  [Trait("Decision", "AC-ATT-0029")]
  public async Task A_leave_request_overlapping_an_existing_one_is_refused()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    await using var context = fixture.CreateContext();
    var leaveTypeId = await SeedLeaveAsync(fixture, context);

    var first = await LeaveHandlerFor(context, fixture).HandleAsync(new SubmitLeaveRequestCommand(
      fixture.CompanyA, fixture.Employee, leaveTypeId,
      new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 11)));

    Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Code : null);

    // Shares exactly one day — the 11th — with the request above.
    var second = await LeaveHandlerFor(context, fixture).HandleAsync(new SubmitLeaveRequestCommand(
      fixture.CompanyA, fixture.Employee, leaveTypeId,
      new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 15)));

    Assert.True(second.IsFailure);

    // The SPECIFIC refusal. An inactive leave type, a missing calendar or an employment-window failure
    // would all be failures too, and would prove nothing about the overlap guard.
    Assert.Equal(LeaveErrors.RequestOverlaps, second.Error);
  }

  // ---- AND THE BOUNDARY, ASSERTED ON ROWS FOR THE SAME REASON AS THE PERIOD HALF.
  [Fact]
  [Trait("Decision", "AC-ATT-0029")]
  public async Task A_day_is_covered_by_exactly_one_leave_request()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    await using var context = fixture.CreateContext();
    var leaveTypeId = await SeedLeaveAsync(fixture, context);

    foreach (var (start, end) in new[]
    {
      (new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 11)),
      (new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18))
    })
    {
      var submitted = await LeaveHandlerFor(context, fixture).HandleAsync(new SubmitLeaveRequestCommand(
        fixture.CompanyA, fixture.Employee, leaveTypeId, start, end));

      Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error.Code : null);
    }

    // Adjacent, not overlapping — so every day belongs to at most one request. Counted against the same
    // predicate `GetOverlappingAsync` uses, because a repository returning a list still cannot tell us
    // whether the INVARIANT held, only what it found.
    foreach (var day in new[] { new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14) })
    {
      var covering = await context.Set<LeaveRequest>()
        .Where(request => request.EmployeeId == fixture.Employee)
        .Where(request => request.StartDate <= day && request.EndDate >= day)
        .CountAsync();

      Assert.Equal(1, covering);
    }
  }

  // ================================================================================================
  // THE DATABASE REFUSES AN IDENTICAL ACTIVE REQUEST (T-150).
  // ================================================================================================
  //
  // **The handler's guard cannot be reached by a test that goes through the handler** — an identical range
  // is an overlap, so the guard refuses first. **This inserts through the context, which is exactly what two
  // concurrent submissions produce**: both pass the guard, and only the engine can refuse the second.
  //
  // ⚠ **It closes the double-click, not the overlap.** Two submissions for 7th–11th and 9th–15th still both
  // commit — a unique index constrains equality on a key, and overlap is a range predicate no index can
  // express (`DEC-L-084`).
  [Fact]
  [Trait("Decision", "AC-ATT-0029")]
  public async Task The_database_refuses_a_second_identical_active_request()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    await using var context = fixture.CreateContext();
    var leaveTypeId = await SeedLeaveAsync(fixture, context);

    context.Set<LeaveRequest>().Add(ActiveRequest(fixture, leaveTypeId));
    await context.SaveChangesAsync();

    // The same employee, the same dates, again — bypassing the handler as a lost race would.
    context.Set<LeaveRequest>().Add(ActiveRequest(fixture, leaveTypeId));

    await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
  }

  // ---- AND IT MUST NOT REFUSE A RESUBMISSION AFTER A REJECTION.
  //
  // The index is filtered to `Status IN (0, 1)` because `GetOverlappingAsync` considers only Submitted and
  // Approved. **An unfiltered unique index would stop an employee rebooking dates that were rejected or
  // cancelled** — ordinary, legitimate, and broken by a constraint meant to catch a double-click. This is
  // the test that holds the filter in place.
  [Fact]
  [Trait("Decision", "AC-ATT-0029")]
  public async Task A_rejected_request_does_not_block_the_same_dates_being_requested_again()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    await using var context = fixture.CreateContext();
    var leaveTypeId = await SeedLeaveAsync(fixture, context);

    var rejected = ActiveRequest(fixture, leaveTypeId);
    Assert.True(rejected.Reject(Guid.NewGuid(), "approver", DateTimeOffset.UtcNow, "not this week").IsSuccess);
    context.Set<LeaveRequest>().Add(rejected);
    await context.SaveChangesAsync();

    context.Set<LeaveRequest>().Add(ActiveRequest(fixture, leaveTypeId));

    // No throw: the rejected row is outside the filter, so the dates are free again.
    Assert.Equal(1, await context.SaveChangesAsync());
  }

  private static LeaveRequest ActiveRequest(AttendanceFixture fixture, Guid leaveTypeId)
  {
    var request = LeaveRequest.Submit(
      fixture.CompanyA, fixture.Employee, leaveTypeId,
      new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 11), workingDaysConsumed: 5m).Value;
    request.TenantId = fixture.Tenant;
    return request;
  }

  private static SubmitLeaveRequestCommandHandler LeaveHandlerFor(
    TenantDbContext context, AttendanceFixture fixture)
  {
    var accessor = new SingleContext(context);

    return new SubmitLeaveRequestCommandHandler(
      new LeaveRequestRepository(accessor),
      new LeaveTypeRepository(accessor),
      new WorkingCalendarRepository(accessor),
      new EmployedRoster(fixture.CompanyA, fixture.Employee),
      new GrantingScope(),
      new SingleContextUnitOfWork(context));
  }

  // The fixture seeds a period and a record and nothing else — a calendar and a leave type are this test's
  // own setup, so they live here rather than being pushed into a shared fixture.
  private static async Task<Guid> SeedLeaveAsync(AttendanceFixture fixture, TenantDbContext context)
  {
    var calendar = WorkingCalendar.Create(
      fixture.CompanyA, "Standard", [DayOfWeek.Friday, DayOfWeek.Saturday], isDefault: true).Value;
    context.Set<WorkingCalendar>().Add(calendar);

    var leaveType = LeaveType.Create(
      fixture.CompanyA, "ANNUAL", "Annual", LeaveBehaviour.PaidWithoutBalance, isSensitive: false).Value;
    context.Set<LeaveType>().Add(leaveType);

    await context.SaveChangesAsync();
    return leaveType.Id;
  }

  // Employed throughout, so the employment window never becomes the reason a request is refused.
  private sealed class EmployedRoster(Guid companyId, Guid employeeId) : IEmployeeRoster
  {
    public Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
      Guid requestedCompanyId, DateTimeOffset fromUtc, DateTimeOffset toUtc,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<EmploymentRecord>>(
      [
        new EmploymentRecord(
          employeeId, companyId, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), null)
      ]);
  }

  private static CreateAttendancePeriodCommandHandler HandlerFor(TenantDbContext context) =>
    new(new AttendancePeriodRepository(new SingleContext(context)),
      new GrantingScope(),
      new SingleContextUnitOfWork(context));

  private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  private sealed class SingleContextUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
  {
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Result.Success(await context.SaveChangesAsync(cancellationToken));

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      new EfTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
      public Task CommitAsync(CancellationToken cancellationToken = default) =>
        transaction.CommitAsync(cancellationToken);

      public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        transaction.RollbackAsync(cancellationToken);

      public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
  }

  // Grants. The subject is period resolution; authorisation has its own coverage, and wiring the real
  // resolver would make IT this fixture's subject — the same reasoning as T-142's GL chain.
  private sealed class GrantingScope : IAttendanceScopeResolver
  {
    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Creating a period resolves no read scope.");

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Creating a period resolves no read scope.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());

    public Result RequirePermission(string permissionName) => Result.Success();

    public bool HasPermission(string permissionName) => true;
  }
}
