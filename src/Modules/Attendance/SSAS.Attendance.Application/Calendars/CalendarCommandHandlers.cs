using SSAS.BuildingBlocks.SharedKernel;
using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Calendars;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Attendance.Application.Calendars;

public sealed record CreateWorkingCalendarCommand(
  Guid CompanyId, string? Name, IReadOnlyList<DayOfWeek>? WeekendDays, bool IsDefault);

public sealed record UpdateWorkingCalendarCommand(
  Guid WorkingCalendarId, string? Name, IReadOnlyList<DayOfWeek>? WeekendDays);

public sealed record AddHolidayCommand(Guid WorkingCalendarId, DateOnly HolidayDate, string? Name);

public sealed record RemoveHolidayCommand(Guid WorkingCalendarId, DateOnly HolidayDate);

public sealed class CreateWorkingCalendarCommandHandler(
  IWorkingCalendarRepository calendars,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateWorkingCalendarCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageCalendars, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var calendar = WorkingCalendar.Create(
      command.CompanyId, command.Name, command.WeekendDays ?? [], command.IsDefault);
    if (calendar.IsFailure)
    {
      return Result.Failure<Guid>(calendar.Error);
    }

    // Company-scoped uniqueness. The database index is the authority; this is the courteous answer, and the
    // index is what makes a race lose rather than duplicate.
    if (await calendars.NameExistsAsync(
      command.CompanyId, calendar.Value.NormalizedName, cancellationToken))
    {
      return Result.Failure<Guid>(WorkingCalendarErrors.DuplicateName);
    }

    await calendars.AddAsync(calendar.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE WORKING CALENDAR RACE: THE GUARD AND THE INDEX NAME THE SAME CONDITION (T-176).
      //
      // `ICalendarRepository.NameExistsAsync` is a read, so two callers can both pass it with the same value and both reach
      // this save. **`the unique index on (TenantId, CompanyId, NormalizedName)` decides it at commit**, and the loser reached
      // `AttendanceApiErrorMapper` with an unmapped `Persistence.UniqueConstraint` — answered 500 for a
      // plain business conflict, while `WorkingCalendarErrors.DuplicateName` sat mapped to 409 and unreturned on
      // this path.
      //
      // ---- ⚠ THE SAME CODE HONESTLY SERVES BOTH, AND THAT IS NOT TRUE OF EVERY RACE.
      //
      // **The race and the pre-check produce an IDENTICAL caller-visible condition** — the name is taken —
      // so one code answers both without lying about either. **Retrying the identical request fails again:**
      // the caller must change the input, not repeat it.
      //
      // That is the opposite of the leave-entitlement race, where a retry finds the winner's row and
      // succeeds, and of the journal reversal, where two different conditions collapse into one exception
      // and neither can be named. **Same 409 in all three; three different things for a client to do.**
      //
      // ⚠ **SOUND ONLY WHILE THIS HANDLER CAN REACH EXACTLY ONE UNIQUE INDEX.** It writes a `WorkingCalendar` and nothing else — `WorkingCalendar.Create` takes no holidays, so 
      // `(WorkingCalendarId, HolidayDate)` is out of reach on this path.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(WorkingCalendarErrors.DuplicateName);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    return Result.Success(calendar.Value.Id);
  }
}

public sealed class UpdateWorkingCalendarCommandHandler(
  IWorkingCalendarRepository calendars,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    UpdateWorkingCalendarCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var calendar = await calendars.GetByIdAsync(command.WorkingCalendarId, cancellationToken);
    if (calendar is null)
    {
      return Result.Failure(WorkingCalendarErrors.NotFound);
    }

    // Authorized against the calendar's OWN company, read from the entity rather than taken from the
    // request. A caller who could name the company would otherwise authorize themselves against one they
    // hold and then edit a calendar belonging to one they do not.
    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageCalendars, calendar.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var renamed = calendar.Rename(command.Name);
    if (renamed.IsFailure)
    {
      return renamed;
    }

    // ---- CHANGING THE WEEKEND IS PERMITTED, AND IT DOES NOT REACH BACKWARDS.
    //
    // `BR-ATT-0003` and `AC-ATT-0019`: a leave request's consumed working days were computed at submission
    // and STORED, so amending the weekend pattern — like adding a holiday — changes what FUTURE requests
    // consume and leaves settled ones exactly as they were.
    //
    // Without that storage this operation would silently rewrite how much leave people had already taken,
    // and therefore balances that had already been reconciled.
    if (command.WeekendDays is not null)
    {
      var changed = calendar.ChangeWeekend(command.WeekendDays);
      if (changed.IsFailure)
      {
        return changed;
      }
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

public sealed class AddHolidayCommandHandler(
  IWorkingCalendarRepository calendars,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    AddHolidayCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var calendar = await calendars.GetByIdAsync(command.WorkingCalendarId, cancellationToken);
    if (calendar is null)
    {
      return Result.Failure(WorkingCalendarErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageCalendars, calendar.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var added = calendar.AddHoliday(command.HolidayDate, command.Name);
    if (added.IsFailure)
    {
      return added;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// Removal is a POST to `holidays/remove` rather than a DELETE, following HR's `manager/remove`: taking a
// date off a maintained list is a named administrative act, and the codebase already spells those that way.
public sealed class RemoveHolidayCommandHandler(
  IWorkingCalendarRepository calendars,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    RemoveHolidayCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var calendar = await calendars.GetByIdAsync(command.WorkingCalendarId, cancellationToken);
    if (calendar is null)
    {
      return Result.Failure(WorkingCalendarErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageCalendars, calendar.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    // ---- THE ROW IS DELETED EXPLICITLY, BEFORE THE AGGREGATE DROPS IT (FP-013 follow-up).
    //
    // The platform sets every foreign key to `Restrict` after the module contributors run, so this
    // module's configured cascade never applies: a holiday removed from the collection would be an orphan
    // nothing deletes, against a non-nullable foreign key EF cannot null.
    //
    // Found by looking rather than by failing — Payroll and GL carry the identical defect, and this module
    // was written after both of them and inherited it from the same misleading comment.
    var holiday = calendar.Holidays.FirstOrDefault(candidate => candidate.HolidayDate == command.HolidayDate);
    if (holiday is not null)
    {
      await calendars.RemoveHolidayAsync(holiday, cancellationToken);
    }

    var removed = calendar.RemoveHoliday(command.HolidayDate);
    if (removed.IsFailure)
    {
      return removed;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
