using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Periods;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Attendance.Application.Periods;

public sealed record CreateAttendancePeriodCommand(
  Guid CompanyId, string? Name, DateOnly StartDate, DateOnly EndDate);

// No body on either transition. Everything each needs is on the period it names, and a body would let a
// caller change WHAT is being closed at the moment of closing — the same reasoning that gives GL's posting
// route and Payroll's approval route no body.
public sealed record CloseAttendancePeriodCommand(Guid AttendancePeriodId);

public sealed record ReopenAttendancePeriodCommand(Guid AttendancePeriodId);

public sealed class CreateAttendancePeriodCommandHandler(
  IAttendancePeriodRepository periods,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateAttendancePeriodCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManagePeriods, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var period = AttendancePeriod.Create(
      command.CompanyId, command.Name, command.StartDate, command.EndDate);
    if (period.IsFailure)
    {
      return Result.Failure<Guid>(period.Error);
    }

    // ---- OVERLAP IS REFUSED, AND THIS IS LOAD-BEARING RATHER THAN TIDY.
    //
    // `IAttendancePeriodRepository.GetCoveringAsync` answers "which period covers this date", and
    // `IAttendanceSummary` resolves a period from a date the caller names (`OD-ATT-0009`). If two periods
    // could cover one date, that resolution would be **arbitrary** — and payroll would consume whichever
    // one the query happened to return first, with nothing anywhere reporting that a choice had been made.
    if (await periods.OverlapsAsync(
      command.CompanyId, command.StartDate, command.EndDate, cancellationToken))
    {
      return Result.Failure<Guid>(AttendancePeriodErrors.OverlapsExistingPeriod);
    }

    await periods.AddAsync(period.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(period.Value.Id);
  }
}

// ================================================================================================
// CLOSING — THE SENSITIVE ACT (OD-ATT-0010, and OD-PAY-0009's reasoning applied).
// ================================================================================================
//
// Gated by `Attendance.Periods.Close`, not by `ManagePeriods`. `OD-PAY-0009` placed payroll's sensitivity at
// APPROVAL rather than calculation because calculation commits nothing while approval is the assertion that
// figures are real. Closing is the analogous act here: **it is the moment the numbers Payroll will consume
// stop moving.**
public sealed class CloseAttendancePeriodCommandHandler(
  IAttendancePeriodRepository periods,
  IAttendanceScopeResolver scope,
  ICurrentUser currentUser,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    CloseAttendancePeriodCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var period = await periods.GetByIdAsync(command.AttendancePeriodId, cancellationToken);
    if (period is null)
    {
      return Result.Failure(AttendancePeriodErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ClosePeriods, period.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var closed = period.Close(currentUser.UserId, DateTimeOffset.UtcNow);
    if (closed.IsFailure)
    {
      return closed;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// ================================================================================================
// REOPENING — AND WHY IT IS SAFE UNDER A RULING THAT SAID "NEVER EDITS".
// ================================================================================================
//
// `OD-ATT-0012` ruled corrections are new adjustment records, never edits, and the analysis package drew the
// reopen arrow as existing only under the ruling that did NOT win. So this handler needs its justification
// stated rather than assumed.
//
// **`AttendanceRecord` is `IAppendOnlyEntity` from creation.** `PreventAppendOnlyMutation` refuses
// `Modified` and `Deleted` for it UNCONDITIONALLY — it does not consult period status, it has no escape
// hatch, and it does not care who holds this permission. **So reopening permits APPENDING and never
// EDITING.** A reopened period cannot rewrite history, by anyone, which is precisely what makes reopening
// an administrative convenience rather than a hole.
//
// It does not silently invalidate a posted payroll journal either. `OD-ATT-0010` makes Payroll refuse an
// OPEN period at approval, so reopening a period a run has already consumed blocks the NEXT approval until
// it closes again, rather than quietly changing what a posted run was based on.
//
// Gated by `ClosePeriods` rather than `ManagePeriods`: whoever may freeze the numbers is whoever may
// unfreeze them.
public sealed class ReopenAttendancePeriodCommandHandler(
  IAttendancePeriodRepository periods,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    ReopenAttendancePeriodCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var period = await periods.GetByIdAsync(command.AttendancePeriodId, cancellationToken);
    if (period is null)
    {
      return Result.Failure(AttendancePeriodErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ClosePeriods, period.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var reopened = period.Reopen();
    if (reopened.IsFailure)
    {
      return reopened;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
