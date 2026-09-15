using SSAS.BuildingBlocks.SharedKernel;
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
  ICurrentUser currentUser,
  IFiscalYearDefinitionLock calendarLock)
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

    // ---- THE TRANSACTION OPENS BEFORE THE CHECKS, AND THE LOCK IS TAKEN INSIDE IT (T-184).
    //
    // **Order is the whole correctness argument here.** Both checks below read state that the write then
    // depends on, so acquiring after them would serialise only the insert and leave the reads racing —
    // the gap would move, not close. `PostJournalCommandHandlers` states the same rule for its own
    // aggregate: reading outside the transaction and writing inside it leaves exactly the window those
    // rules exist to close.
    //
    // ⚠ **`DEC-L-084` IS UNTOUCHED AND NO CONSTRAINT HAS APPEARED.** SQL Server still cannot express
    // range non-overlap, and `CalendarConfigurations` still deliberately carries no index on
    // `(StartUtc, EndUtc)`. **`OverlapsExistingAsync` remains the only thing that decides overlap** — the
    // lock makes its answer survive concurrency, it does not replace it.
    //
    // **What an overlap costs is why this is worth a transaction on a ledger write path.**
    // `GetCoveringAsync` uses `FirstOrDefaultAsync`, and posting numbers each journal from the year that
    // call returns — so two overlapping years scatter one date's postings across two numbering sequences,
    // arbitrarily. See `IFiscalYearDefinitionLock`.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var locked = await calendarLock.AcquireAsync(
      currentTenant.TenantId.Value, command.CompanyId, cancellationToken);
    if (locked.IsFailure)
    {
      return Result.Failure<Guid>(locked.Error);
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
    if (saved.IsFailure)
    {
      // ---- THE FISCAL-YEAR CODE RACE (T-177).
      //
      // `IFiscalCalendarRepository.CodeExistsAsync` is a read, so two callers can pass it with the same value and both reach this
      // save. **`UX_GlFiscalYears_Tenant_Company_Code` decides it at commit**, and the loser reached `GlApiErrorMapper` with an
      // unmapped `Persistence.UniqueConstraint` — answered 500 for a plain business conflict, while
      // `CalendarErrors.DuplicateCode` sat mapped to 409 and unreturned on this path.
      //
      // ---- ⚠ THE SAME CODE HONESTLY SERVES THE CHECK AND THE RACE.
      //
      // **Both produce an identical caller-visible condition** — that code is taken — so one code answers
      // both without lying about either. **Retrying the identical request fails again**; the caller must
      // change the code. That is not the leave-entitlement shape, where a retry finds the winner's row
      // and succeeds, nor the journal reversal, where two conditions collapse and neither can be named.
      //
      // ⚠ **REACHES EXACTLY ONE UNIQUE INDEX, WHICH IS WHY IT MAY NAME ONE.** It writes a `FiscalYear` and its child periods, and `FiscalPeriod` carries NO unique index — only
      // `IX_GlFiscalPeriods_Year_Start`, which is not unique. So one index is reachable.
      //
      // ---- ⚠⚠ THIS NAMES THE CODE RACE ONLY. THE OVERLAP RACE REMAINS OPEN AND IS NOT CLOSED HERE.
      //
      // This handler runs TWO guards: `CodeExistsAsync` and `OverlapsExistingAsync`. **Only the first
      // has a database backstop.** `CalendarConfigurations` records why there is deliberately no index
      // on `(StartUtc, EndUtc)`: SQL Server cannot express "these ranges must not overlap" at all
      // (`DEC-L-084`), so `OverlapsExistingAsync` is the ONLY enforcement and two concurrent callers can
      // still define overlapping years.
      //
      // **So the error below must be the CODE conflict specifically, and must not read as "fiscal year
      // conflict" generally.** A translation that closes one race while appearing to close two is worse
      // than the 500 it replaces: **a 500 invites investigation and a confident 409 does not.**
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(CalendarErrors.DuplicateCode);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    // Commit releases the lock — `@LockOwner = 'Transaction'` means there is no separate release to forget.
    await transaction.CommitAsync(cancellationToken);

    return Result.Success(year.Value.Id);
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
  IFiscalPeriodPostingLock postingLock,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
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

    // ---- THE EXCLUSIVE SIDE OF THE POSTING FENCE (249). See `IFiscalPeriodPostingLock`.
    //
    // Taking it here DRAINS IN-FLIGHT POSTERS: a poster holds the shared resource from before its period
    // read until its commit, so this waits for every posting already under way and blocks any new one
    // from starting while the state changes.
    //
    // ⚠ AND THE PERIOD WAS READ ABOVE, BEFORE THIS LOCK, WHICH IS DELIBERATE AND IS NOT THE ORDERING
    // DEFECT THE POSTERS WERE FIXED FOR. The two mechanisms cover DIFFERENT PAIRS:
    //
    //   the FENCE serialises POSTER against CLOSER, inside overlapping transactions;
    //   the ROWVERSION catches a STALE period read across SEPARATE requests -- `command.RowVersion`
    //   below is the caller's copy, and a concurrent state change loses at save.
    //
    // So this handler's own read is protected by the token, and the fence exists here only to drain
    // posters. NEITHER MECHANISM MAKES THE OTHER REDUNDANT and neither may be removed as tidying.
    //
    // The company is not known until the year is read, which is why the lock cannot precede that read:
    // the resource is company-scoped and `SetFiscalPeriodStateCommand` carries only a period id.
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var fenced = await postingLock.AcquireForStateChangeAsync(
      tenantId, year.CompanyId, cancellationToken);

    if (fenced.IsFailure)
    {
      return fenced;
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

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);

    return Result.Success();
  }
}
