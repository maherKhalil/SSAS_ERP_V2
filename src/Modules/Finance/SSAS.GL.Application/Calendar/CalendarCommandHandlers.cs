using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Calendar;

namespace SSAS.GL.Application.Calendar;

// THE FISCAL CALENDAR'S WRITE PATH (REQ-GL-0009, REQ-GL-0010, OD-GL-0004).
//
// EVERY COMMAND HERE CARRIES A CompanyId AND CALLS `AuthorizeAsync`, which is the exact opposite of the
// account handlers next door — and the difference is `OD-GL-0004` against `OD-GL-0003`. The calendar is
// company-owned, so `FiscalYear` is `ICompanyOwnedEntity`, so **closing a period is a company-scoped
// write** and `TenantDbContext.ApplyCompanyRulesAsync` runs `AuthorizeCurrentCompanyAsync` before anything
// reaches SQL. The check in this file is the application's; the one at the write boundary is the platform's,
// and neither substitutes for the other.

public sealed record DefineFiscalYearCommand(
  Guid CompanyId,
  string Code,
  DateTimeOffset StartUtc,
  DateTimeOffset EndUtc,
  IReadOnlyList<FiscalPeriodDefinition> Periods);

public sealed record FiscalPeriodDefinition(string Name, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed class DefineFiscalYearCommandHandler(
  IFiscalCalendarRepository calendar,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleAsync(
    DefineFiscalYearCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(GlScopeErrors.InvalidActor);
    }

    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.ManagePeriods, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    // The aggregate validates contiguity across the whole set — see `FiscalYear.Create`. It is checked
    // there rather than here because it is a property of the SET, and a handler that walked the periods
    // itself would be a second implementation of an invariant the aggregate already owns.
    var year = FiscalYear.Create(
      command.Code,
      command.StartUtc,
      command.EndUtc,
      [.. command.Periods.Select(period => (period.Name, period.StartUtc, period.EndUtc))]);

    if (year.IsFailure)
    {
      return Result.Failure<Guid>(year.Error);
    }

    if (await calendar.CodeExistsAsync(command.CompanyId, year.Value.Code, cancellationToken))
    {
      return Result.Failure<Guid>(CalendarErrors.DuplicateCode);
    }

    // ---- YEARS MUST NOT OVERLAP, AND THIS IS THE ONLY PLACE THAT CAN SAY SO.
    //
    // Unlike the code conflict above, no unique index can express this: overlap is a range predicate across
    // rows, not an equality on a key. So there is no database backstop, and two concurrent definitions of
    // adjacent-but-overlapping years could both pass. Recorded rather than papered over — the exposure is
    // small (defining a fiscal year is rare and deliberate) and the alternative is a lock held across a
    // human-scale operation.
    if (await calendar.OverlapsExistingAsync(
      command.CompanyId, year.Value.StartUtc, year.Value.EndUtc, cancellationToken))
    {
      return Result.Failure<Guid>(CalendarErrors.OverlappingYear);
    }

    year.Value.CompanyId = command.CompanyId;
    await calendar.AddAsync(year.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(year.Value.Id);
  }
}

// `REQ-GL-0010`. Close and reopen in one handler, because they are one transition read in two directions
// and `BR-GL-0003` gives the closed state its only stated consequence. Reopening was unstated by every
// existing rule and was settled by the build ruling as an EXPLICIT operation — so it is an action someone
// took and can be audited, never a side effect of something else.
public sealed record SetFiscalPeriodStateCommand(Guid FiscalPeriodId, bool IsOpen, byte[]? RowVersion);

public sealed class SetFiscalPeriodStateCommandHandler(
  IFiscalCalendarRepository calendar,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    SetFiscalPeriodStateCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    var period = await calendar.GetPeriodAsync(command.FiscalPeriodId, cancellationToken);
    if (period is null)
    {
      return Result.Failure(CalendarErrors.PeriodNotFound);
    }

    // ---- THE COMPANY COMES FROM THE YEAR, NOT FROM THE CALLER.
    //
    // A period names no company; its year does. Loading the year to learn which company to authorize
    // against is why the permission check happens after the lookup here and before it in the account
    // handlers — the caller cannot be trusted to say which company a period belongs to, because saying so
    // is exactly how they would reach one they may not.
    var year = await calendar.GetByIdAsync(period.FiscalYearId, cancellationToken);
    if (year is null)
    {
      return Result.Failure(CalendarErrors.YearNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.ClosePeriods, year.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var transition = command.IsOpen ? period.Reopen() : period.Close();
    if (transition.IsFailure)
    {
      return transition;
    }

    if (command.RowVersion is { Length: > 0 })
    {
      period.RowVersion = command.RowVersion;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
