using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.SharedKernel;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Application.Journals;

// ================================================================================================
// POSTING — WHERE ALL THREE BUSINESS RULES MEET, IN ONE TRANSACTION (REQ-GL-0001).
// ================================================================================================
//
// `BR-GL-0001` the journal balances, `BR-GL-0003` the period is open, `BR-GL-0004` every account is active.
// Two of the three are facts about OTHER aggregates, read LIVE, and all three are checked inside the
// writing transaction — because a draft prepared an hour ago tells you nothing about whether the period
// closed or an account was deactivated in the meantime.
//
// ---- THE DRAFT IS READ AND A SEPARATE AGGREGATE IS CREATED. NOTHING IS PROMOTED IN PLACE.
//
// `JournalEntry.Post` is `internal` and takes the draft; the draft is then discarded. That one-way step is
// what lets `JournalEntry` carry `IAppendOnlyEntity` FROM CREATION, so the write boundary refuses every
// later modification by whatever path attempts it — repository, direct attach, or a path nobody has written
// yet. `OD-GL-0007` weighed exactly this against a single status-carrying aggregate and chose the guarantee.
public sealed record PostJournalDraftCommand(Guid JournalDraftId);

public sealed class PostJournalDraftCommandHandler(
  IJournalDraftRepository drafts,
  IJournalEntryRepository journals,
  IAccountRepository accounts,
  IFiscalCalendarRepository calendar,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleAsync(
    PostJournalDraftCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(GlScopeErrors.InvalidActor);
    }

    var draft = await drafts.GetByIdAsync(command.JournalDraftId, cancellationToken);
    if (draft is null)
    {
      return Result.Failure<Guid>(JournalErrors.DraftNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.PostJournals, draft.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    // ---- THE TRANSACTION OPENS BEFORE THE CHECKS, NOT AFTER THEM.
    //
    // `BR-GL-0003` and `BR-GL-0004` are read-then-act: a period read as open, or an account read as active,
    // must still be so when the row is written. Reading outside the transaction and writing inside it would
    // leave exactly the window those rules exist to close, and `TS-GL-0011` asserts the closed-between case.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    // `BR-GL-0001` and the two-line minimum — the only checks the draft can make alone.
    var postable = draft.EnsurePostable();
    if (postable.IsFailure)
    {
      return Result.Failure<Guid>(postable.Error);
    }

    var period = await ResolvePeriodAsync(draft, cancellationToken);
    if (period.IsFailure)
    {
      return Result.Failure<Guid>(period.Error);
    }

    var accountsChecked = await EnsureAccountsCanReceiveAsync(draft, cancellationToken);
    if (accountsChecked.IsFailure)
    {
      return Result.Failure<Guid>(accountsChecked.Error);
    }

    var number = await journals.NextJournalNumberAsync(
      draft.CompanyId, period.Value.FiscalYearId, cancellationToken);

    var entry = JournalEntry.Post(
      draft,
      period.Value.FiscalYearId,
      period.Value.Id,
      number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    await journals.AddAsync(entry, cancellationToken);

    // The draft has served its purpose and is removed in the SAME transaction. Leaving it would let the
    // same work be posted twice, which no uniqueness rule would catch: a second posting of the same draft
    // is a different journal number and a perfectly valid-looking duplicate of the books.
    drafts.Remove(draft);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE JOURNAL-NUMBER RACE, NAMED HERE RATHER THAN IN THE MAPPER (T-165).
      //
      // `NextJournalNumberAsync` is a read-then-write, and `UX_GlJournalEntries_Tenant_Company_Year_Number`
      // is what makes the race unwinnable. **Before this, the loser answered 500**: the unit of work
      // returns the generic `Persistence.UniqueConstraint`, `GlApiErrorMapper` has no arm for it, and the
      // default is `WriteFailure` — while `JournalErrors.NumberConflict`, mapped to 409, was returned by
      // nothing.
      //
      // ⚠ **TRANSLATED HERE AND NOT IN THE MAPPER, AND THAT IS `DEC-DEP-0027` LITERALLY.** GL has SIX
      // unique indexes. A module-wide arm would answer *"a journal with this number already exists"* to a
      // duplicate account code, a duplicate fiscal-year code, and — worst — a double-reversal race, which
      // owns `JournalErrors.AlreadyReversed`. **A confident wrong answer is what the 500 default exists to
      // prevent.** Only the caller knows which index it could have hit.
      //
      // **This handler can hit exactly one.** `UX_GlJournalLines_Entry_LineNumber` is deterministic from
      // the draft, and `UX_GlJournalEntries_OneReversalPerOriginal` is FILTERED to
      // `ReversesJournalEntryId IS NOT NULL`, which a posting never sets.
      //
      // ⚠ **THAT FILTER IS LOAD-BEARING FOR THIS TRANSLATION, IN A FILE NOBODY WOULD THINK TO CHECK.**
      //
      // `JournalConfigurations.cs` declares `UX_GlJournalEntries_OneReversalPerOriginal` with
      // `.HasFilter("[ReversesJournalEntryId] IS NOT NULL")`. **Remove that filter and this translation
      // becomes wrong**: every posting would then contend on the index's NULLs, and the loser would be
      // told a journal number already exists when the real collision was elsewhere — the exact confident
      // wrong answer that keeping this out of the mapper avoided.
      //
      // **A schema filter and a handler's correctness are coupled here.** Stated because the coupling is
      // silent, and "tidying" an index filter is a plausible unrelated change.
      // ⚠ COMPARED ON THE CODE STRING, BECAUSE `ADR-012` FORBIDS THE TYPE.
      //
      // `Persistence.UniqueConstraint` is declared as `IdentityAccessErrors.UniqueConstraintViolation` in
      // `SSAS.Platform.Domain`, which GL may not reference. **The code STRING is the only vocabulary the
      // two share across that boundary**, and GL is the first module to need it — no other handler in
      // `src/Modules` compares on a `Persistence.*` code today.
      //
      // **A shared constant in BuildingBlocks would be better and is not mine to introduce**: it would be
      // a new cross-module vocabulary, which is an architecture decision rather than a defect fix.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(JournalErrors.NumberConflict);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(entry.Id);
  }

  private async Task<Result<FiscalPeriod>> ResolvePeriodAsync(
    JournalDraft draft, CancellationToken cancellationToken)
  {
    var year = await calendar.GetCoveringAsync(draft.CompanyId, draft.EntryDateUtc, cancellationToken);
    if (year is null)
    {
      return Result.Failure<FiscalPeriod>(CalendarErrors.PeriodNotFound);
    }

    // Resolved from the ENTRY DATE, never supplied by the caller (`AC-GL-0002`). A caller who could name
    // the period could post into one the date does not belong to, which would make `BR-GL-0003`
    // unenforceable by inspection — the closed-period check would be guarding a field the caller chose.
    return year.ResolveOpenPeriodFor(draft.EntryDateUtc);
  }

  private async Task<Result> EnsureAccountsCanReceiveAsync(
    JournalDraft draft, CancellationToken cancellationToken)
  {
    var accountIds = draft.Lines.Select(line => line.AccountId).Distinct().ToArray();
    var loaded = await accounts.GetManyAsync(accountIds, cancellationToken);
    var byId = loaded.ToDictionary(account => account.Id);

    // ---- ORDERED BY LINE NUMBER SO THE REFUSAL IS STABLE.
    //
    // A journal naming two inactive accounts must always name the SAME one in its error, or two identical
    // requests produce two different messages and a user chasing the second discovers the first. Iterating
    // the lines in order rather than the loaded set gives that for free.
    foreach (var line in draft.Lines.OrderBy(line => line.LineNumber))
    {
      if (!byId.TryGetValue(line.AccountId, out var account))
      {
        // Out of the tenant, or simply gone. Reported as NOT FOUND rather than forbidden: telling a caller
        // that an account exists but is not theirs leaks the chart one probe at a time.
        return Result.Failure(Domain.Accounts.AccountErrors.NotFound);
      }

      var receivable = account.EnsureCanReceiveTransactions();
      if (receivable.IsFailure)
      {
        return receivable;
      }
    }

    return Result.Success();
  }
}

// ---- REVERSAL (REQ-GL-0004, OD-GL-0006).
//
// The correction mechanism `BR-GL-0002` implies but never names. The reversal is built FROM THE ORIGINAL —
// debits become credits, line for line — so it cannot silently differ from what it claims to reverse, and
// `ReversesJournalEntryId` makes the pair discoverable from either side.
//
// "Reversed" is never written onto the original. That would mean modifying an append-only row, which the
// write boundary refuses; the fact is derived from the reversal's existence instead.
public sealed record ReverseJournalCommand(Guid JournalEntryId, DateTimeOffset ReversalDateUtc, string Description);

public sealed class ReverseJournalCommandHandler(
  IJournalEntryRepository journals,
  IFiscalCalendarRepository calendar,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleAsync(
    ReverseJournalCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(GlScopeErrors.InvalidActor);
    }

    var original = await journals.GetByIdAsync(command.JournalEntryId, cancellationToken);
    if (original is null)
    {
      return Result.Failure<Guid>(JournalErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.ReverseJournals, original.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    // ---- CHECKED HERE, AND MADE UNWINNABLE BY A FILTERED UNIQUE INDEX.
    //
    // This read gives the user a named refusal for the double-click case. Two concurrent requests can both
    // read "not yet reversed", so the index on (TenantId, ReversesJournalEntryId) is what makes the rule
    // true — the second writer loses at commit rather than doubling the correction and leaving the books
    // wrong by the original amount.
    if (await journals.ReversalExistsAsync(original.Id, cancellationToken))
    {
      return Result.Failure<Guid>(JournalErrors.AlreadyReversed);
    }

    // The reversal lands in the period covering ITS OWN date, not the original's. Reversing into a closed
    // period is exactly what `BR-GL-0003` forbids, and a correction dated today belongs in today's period —
    // which is also why the caller supplies the date rather than inheriting it.
    var year = await calendar.GetCoveringAsync(
      original.CompanyId, command.ReversalDateUtc, cancellationToken);
    if (year is null)
    {
      return Result.Failure<Guid>(CalendarErrors.PeriodNotFound);
    }

    var period = year.ResolveOpenPeriodFor(command.ReversalDateUtc);
    if (period.IsFailure)
    {
      return Result.Failure<Guid>(period.Error);
    }

    var number = await journals.NextJournalNumberAsync(
      original.CompanyId, year.Id, cancellationToken);

    var reversal = JournalEntry.Reverse(
      original,
      period.Value.Id,
      number.ToString(System.Globalization.CultureInfo.InvariantCulture),
      command.ReversalDateUtc,
      command.Description);

    await journals.AddAsync(reversal, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<Guid>(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(reversal.Id);
  }
}
