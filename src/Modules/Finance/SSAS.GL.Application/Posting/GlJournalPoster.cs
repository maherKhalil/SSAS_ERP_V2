using System.Globalization;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Contracts.Posting;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Application.Posting;

// ================================================================================================
// GL'S SIDE OF THE PAYROLL BOUNDARY. A THIN ADAPTER OVER GL'S OWN POSTING PATH — DELIBERATELY.
// ================================================================================================
//
// `OD-GL-0009` ruled that nothing posts to GL in V1 and named Payroll as the first inbound poster. This is
// that poster's landing site, and the single most important property of this class is what it does NOT do:
//
//   **It does not implement posting. It reuses it.**
//
// Every invariant a user-posted journal passes, a payroll-posted journal passes, through the same code:
// `JournalDraft.EnsurePostable` for `BR-GL-0001` and the two-line minimum, `FiscalYear.ResolveOpenPeriodFor`
// for `BR-GL-0003`, `Account.EnsureCanReceiveTransactions` for `BR-GL-0004`, and
// `IJournalEntryRepository.NextJournalNumberAsync` for `BR-GL-0005`. A second posting path that
// re-implemented any of those would be a second set of books' worth of rules to keep in step — and the one
// that drifted would be the one nobody was watching, because it has no UI.
//
// The draft it builds is TRANSIENT: it is never added to the repository, so nothing persists it and nothing
// can post it twice. It exists because `JournalEntry.Post` takes one, and because taking one is how the
// balance rule gets enforced on payroll's lines by the type that owns that rule.
//
// ---- WHY THERE IS NO GL PERMISSION CHECK HERE, WHICH IS THE THING TO SCRUTINISE.
//
// A payroll operator holds `Payroll.Runs.Post` (`OD-PAY-0009`), not `GL.Journals.Post`. Demanding a GL
// permission here would mean running payroll required ledger authority — the same coupling `DEC-PAY-0017`
// rejected on the HR side, and for the same reason: the two grants are deliberately separate.
//
// What is NOT skipped is the company boundary. `TenantDbContext.ApplyCompanyRulesAsync` authorizes every
// company-owned write at save time against the trusted company execution context, so a payroll post into an
// unreachable company is refused by the write boundary itself. That is a STRUCTURAL guarantee rather than a
// check this class could write, and it is stronger than one: it cannot be forgotten by a future method here.
//
// ---- REFUSALS ARE ANSWERS, NOT EXCEPTIONS.
//
// A closed period and an unavailable account come back as `JournalPostingOutcome` values because the
// caller's response to them is to refuse a state transition, not to fail. Infrastructure faults still throw.
// That distinction is the entire reason the contract declares a closed enum rather than reusing `Result<T>`.
public sealed class GlJournalPoster(
  IJournalEntryRepository journals,
  IAccountRepository accounts,
  IFiscalCalendarRepository calendar,
  ITenantUnitOfWork unitOfWork) : IJournalPoster
{
  public async Task<JournalPostingOutcome> PostAsync(
    JournalPostingRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    // ---- THE TRANSACTION OPENS BEFORE THE CHECKS, exactly as PostJournalDraftCommandHandler does.
    //
    // `BR-GL-0003` and `BR-GL-0004` are read-then-act: a period read as open, or an account read as active,
    // must still be so when the row is written. Reading outside the transaction and writing inside it leaves
    // exactly the window those rules exist to close.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var draft = JournalDraft.Create(request.EntryDateUtc, request.Description, request.Reference);
    if (draft.IsFailure)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.Unbalanced, draft.Error.Message);
    }

    draft.Value.CompanyId = request.CompanyId;

    var lines = draft.Value.ReplaceLines(
      [.. request.Lines.Select(line => (line.AccountId, line.Debit, line.Credit, line.Description))]);
    if (lines.IsFailure)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.Unbalanced, lines.Error.Message);
    }

    // `BR-GL-0001` and the two-line minimum, enforced by the type that owns the rule. If Payroll ever
    // reaches this, Payroll has a calculation defect — which is why the contract carries the status at all.
    var postable = draft.Value.EnsurePostable();
    if (postable.IsFailure)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.Unbalanced, postable.Error.Message);
    }

    var covering = await calendar.GetCoveringAsync(request.CompanyId, draft.Value.EntryDateUtc, cancellationToken);

    // A `Failure` is AMBIGUITY - more than one fiscal year covers this date (T-187).
    // **Payroll posts through this path too**, so the same broken calendar reaches the
    // ledger from two directions and both must refuse rather than pick.
    if (covering.IsFailure)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.PeriodNotFound);
    }

    var year = covering.Value;
    if (year is null)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.PeriodNotFound);
    }

    // Resolved from the ENTRY DATE, never supplied — the contract has no field for a period precisely so
    // this cannot be bypassed.
    var period = year.ResolveOpenPeriodFor(draft.Value.EntryDateUtc);
    if (period.IsFailure)
    {
      // The distinction the caller needs: a CLOSED period names itself so `OD-PAY-0014`'s refusal can name
      // it; an absent one is a different remedy (define the calendar rather than reopen a period).
      var closed = year.Periods.FirstOrDefault(p => p.Covers(draft.Value.EntryDateUtc));
      return closed is null
        ? JournalPostingOutcome.Refused(JournalPostingStatus.PeriodNotFound)
        : JournalPostingOutcome.Closed(closed.Name);
    }

    var accountsChecked = await EnsureAccountsCanReceiveAsync(draft.Value, cancellationToken);
    if (accountsChecked is not null)
    {
      return accountsChecked;
    }

    var number = await journals.NextJournalNumberAsync(
      request.CompanyId, period.Value.FiscalYearId, cancellationToken);

    var entry = JournalEntry.Post(
      draft.Value,
      period.Value.FiscalYearId,
      period.Value.Id,
      number.ToString(CultureInfo.InvariantCulture));

    await journals.AddAsync(entry, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.Unbalanced, saved.Error.Message);
    }

    await transaction.CommitAsync(cancellationToken);
    return JournalPostingOutcome.Success(entry.Id);
  }

  public async Task<JournalPostingOutcome> ReverseAsync(
    JournalReversalRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var original = await journals.GetByIdAsync(request.JournalEntryId, cancellationToken);
    if (original is null)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.ReversalTargetUnavailable);
    }

    var covering = await calendar.GetCoveringAsync(original.CompanyId, request.ReversalDateUtc, cancellationToken);

    // A `Failure` is AMBIGUITY - more than one fiscal year covers this date (T-187).
    // **Payroll posts through this path too**, so the same broken calendar reaches the
    // ledger from two directions and both must refuse rather than pick.
    if (covering.IsFailure)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.PeriodNotFound);
    }

    var year = covering.Value;
    if (year is null)
    {
      return JournalPostingOutcome.Refused(JournalPostingStatus.PeriodNotFound);
    }

    var period = year.ResolveOpenPeriodFor(request.ReversalDateUtc);
    if (period.IsFailure)
    {
      var closed = year.Periods.FirstOrDefault(p => p.Covers(request.ReversalDateUtc));
      return closed is null
        ? JournalPostingOutcome.Refused(JournalPostingStatus.PeriodNotFound)
        : JournalPostingOutcome.Closed(closed.Name);
    }

    var number = await journals.NextJournalNumberAsync(
      original.CompanyId, period.Value.FiscalYearId, cancellationToken);

    // Built FROM the original, line for line — the caller supplies only a date and a description, so a
    // reversal cannot silently differ from what it claims to reverse. And the original is never modified:
    // "reversed" is derived from the reversal's existence, because writing it back would be a mutation the
    // append-only boundary refuses.
    var reversal = JournalEntry.Reverse(
      original,
      period.Value.Id,
      number.ToString(CultureInfo.InvariantCulture),
      request.ReversalDateUtc,
      request.Description);

    await journals.AddAsync(reversal, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return JournalPostingOutcome.Refused(
        JournalPostingStatus.ReversalTargetUnavailable, saved.Error.Message);
    }

    await transaction.CommitAsync(cancellationToken);
    return JournalPostingOutcome.Success(reversal.Id);
  }

  // A QUERY. No transaction, no write, no reservation — see the contract's own comment on why this exists
  // and what it deliberately does not promise.
  public async Task<PostingWindow> InspectPostingWindowAsync(
    Guid companyId, DateTimeOffset entryDateUtc, CancellationToken cancellationToken = default)
  {
    var covering = await calendar.GetCoveringAsync(companyId, entryDateUtc, cancellationToken);

    // ---- ⚠ THIS ONE IS A QUERY, AND ITS ANSWER IS THE ONE THAT WORRIES ME MOST (T-187).
    //
    // A `Failure` is AMBIGUITY — more than one fiscal year covers this date. **`PostingWindow` has no
    // vocabulary for that**: its statuses describe a period's state, not a calendar's integrity, and
    // widening the contract would change what every caller must handle for a condition none of them can
    // remedy.
    //
    // So it answers `PeriodNotFound`, which is **not the honest answer and is the safe one**. This is a
    // read that Payroll uses to decide whether a run may post; reporting "no window" stops the run, where
    // reporting a usable window would let payroll post into an arbitrarily chosen year. **The WRITE paths
    // above return the real error; this query degrades to a refusal rather than inventing a status.**
    //
    // If a caller ever needs to tell the two apart here, `PostingWindowStatus` is where the distinction
    // belongs — not a second query.
    if (covering.IsFailure)
    {
      return new PostingWindow(PostingWindowStatus.PeriodNotFound, null);
    }

    var year = covering.Value;
    if (year is null)
    {
      return new PostingWindow(PostingWindowStatus.PeriodNotFound, null);
    }

    var period = year.ResolveOpenPeriodFor(entryDateUtc);
    if (period.IsSuccess)
    {
      // Identity and bounds travel with the status: PayrollPeriod.CreateAlignedTo needs them, and asking
      // twice would be two answers about one calendar with a race between them.
      return new PostingWindow(
        PostingWindowStatus.Open,
        period.Value.Name,
        period.Value.Id,
        period.Value.StartUtc,
        period.Value.EndUtc);
    }

    var closed = year.Periods.FirstOrDefault(p => p.Covers(entryDateUtc));
    return closed is null
      ? new PostingWindow(PostingWindowStatus.PeriodNotFound, null)
      : new PostingWindow(PostingWindowStatus.PeriodClosed, closed.Name);
  }

  // Returns null when every account can receive, so the caller reads it as "no refusal". Ordered by line
  // number so two identical requests name the SAME account — an unstable refusal sends a user chasing the
  // second problem while the first is still there.
  private async Task<JournalPostingOutcome?> EnsureAccountsCanReceiveAsync(
    JournalDraft draft, CancellationToken cancellationToken)
  {
    var accountIds = draft.Lines.Select(line => line.AccountId).Distinct().ToArray();
    var loaded = await accounts.GetManyAsync(accountIds, cancellationToken);
    var byId = loaded.ToDictionary(account => account.Id);

    foreach (var line in draft.Lines.OrderBy(line => line.LineNumber))
    {
      if (!byId.TryGetValue(line.AccountId, out var account))
      {
        return JournalPostingOutcome.Refused(
          JournalPostingStatus.AccountUnavailable, AccountErrors.NotFound.Message);
      }

      var receivable = account.EnsureCanReceiveTransactions();
      if (receivable.IsFailure)
      {
        return JournalPostingOutcome.Refused(
          JournalPostingStatus.AccountUnavailable, receivable.Error.Message);
      }
    }

    return null;
  }
}
