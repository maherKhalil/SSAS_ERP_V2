using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Calendars;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.SharedKernel;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Attendance.Tests.Leave;

// ==================================================================================================
// A LOST UNIQUENESS RACE IS A 409, NOT A 500 (T-176).
// ==================================================================================================
//
// `NameExistsAsync` and `CodeExistsAsync` are reads, so two callers can both pass them with the same value
// and both reach the save. The unique index decides it at commit — and the loser reached
// `AttendanceApiErrorMapper` with an unmapped `Persistence.UniqueConstraint`, answered 500, while the
// module's own conflict codes sat mapped to 409 and unreturned on that path.
//
// ---- ⚠ THE SAME CODE HONESTLY SERVES THE CHECK AND THE RACE, WHICH IS NOT TRUE EVERYWHERE.
//
// **Both produce an identical caller-visible condition** — the name is taken — so one code answers both
// without lying about either. **Retrying the identical request fails again**; the caller must change the
// input.
//
// That differs from the leave-entitlement race, where a retry finds the winner's row and succeeds, and from
// the journal reversal, where two conditions collapse into one exception and neither can be named.
// **Same 409 in all three, three different things for a client to do** — which is why each site says which
// it is rather than saying "conflict".
public sealed class AttendanceConflictRaceTests
{
  private static readonly Guid Company = Guid.NewGuid();

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_lost_calendar_name_race_is_named_rather_than_a_write_failure()
  {
    var handler = new CreateWorkingCalendarCommandHandler(
      new NoCalendars(),
      new GrantingScope(),
      new FailingUnitOfWork(new Error(PersistenceErrorCodes.UniqueConstraint, "Unique index violated.")));

    var result = await handler.HandleAsync(
      new CreateWorkingCalendarCommand(Company, "Standard", [DayOfWeek.Friday], true));

    Assert.True(result.IsFailure);
    Assert.Equal(WorkingCalendarErrors.DuplicateName, result.Error);
  }

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_lost_leave_type_code_race_is_named_rather_than_a_write_failure()
  {
    var handler = new CreateLeaveTypeCommandHandler(
      new NoLeaveTypes(),
      new GrantingScope(),
      new FailingUnitOfWork(new Error(PersistenceErrorCodes.UniqueConstraint, "Unique index violated.")));

    var result = await handler.HandleAsync(
      new CreateLeaveTypeCommand(Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false));

    Assert.True(result.IsFailure);
    Assert.Equal(LeaveErrors.DuplicateLeaveTypeCode, result.Error);
  }

  // ---- THE CONTROLS, AND WITHOUT THEM BOTH TESTS ABOVE PROVE ALMOST NOTHING.
  //
  // A handler that turned EVERY save failure into a conflict would pass them and be badly wrong: a storage
  // outage would be reported to an operator as a duplicate name.
  [Theory]
  [InlineData("calendar")]
  [InlineData("leaveType")]
  public async Task Any_other_save_failure_is_passed_through_unchanged(string subject)
  {
    var other = new Error(PersistenceErrorCodes.WriteFailure, "The write did not complete.");
    var unitOfWork = new FailingUnitOfWork(other);

    var result = subject == "calendar"
      ? await new CreateWorkingCalendarCommandHandler(new NoCalendars(), new GrantingScope(), unitOfWork)
        .HandleAsync(new CreateWorkingCalendarCommand(Company, "Standard", [DayOfWeek.Friday], true))
      : await new CreateLeaveTypeCommandHandler(new NoLeaveTypes(), new GrantingScope(), unitOfWork)
        .HandleAsync(new CreateLeaveTypeCommand(Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false));

    Assert.True(result.IsFailure);
    Assert.Equal(other, result.Error);
  }

  private sealed class NoCalendars : IWorkingCalendarRepository
  {
    public Task<WorkingCalendar?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
      Task.FromResult<WorkingCalendar?>(null);

    public Task<WorkingCalendar?> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult<WorkingCalendar?>(null);

    public Task<bool> NameExistsAsync(
      Guid companyId, string normalizedName, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(WorkingCalendar calendar, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

    public Task RemoveHolidayAsync(CalendarHoliday holiday, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Creating a calendar removes no holiday.");
  }

  private sealed class NoLeaveTypes : ILeaveTypeRepository
  {
    public Task<LeaveType?> GetByIdAsync(Guid leaveTypeId, CancellationToken cancellationToken = default) =>
      Task.FromResult<LeaveType?>(null);

    public Task<bool> CodeExistsAsync(
      Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(LeaveType leaveType, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class GrantingScope : IAttendanceScopeResolver
  {
    public Result RequirePermission(string permissionName) => Result.Success();

    public bool HasPermission(string permissionName) => true;

    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Creating resolves no read scope.");

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Creating resolves no read scope.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());
  }

  private sealed class FailingUnitOfWork(Error error) : ITenantUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Failure<int>(error));

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These handlers open no transaction.");
  }
}
