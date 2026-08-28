using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Infrastructure.Persistence;

// Every query goes through `ITenantDbContextAccessor`, which resolves the tenant's context and applies the
// tenant global filter. None of these methods names a `TenantId` for that reason — adding one would be a
// second source of truth for an invariant the context already enforces.
internal sealed class AccountRepository(ITenantDbContextAccessor contextAccessor) : IAccountRepository
{
  public async Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<Account>()
      .FirstOrDefaultAsync(account => account.Id == accountId, cancellationToken);
  }

  // Compared on the NORMALIZED column, which is binary-collated, so the database decides what counts as the
  // same code rather than the caller's culture.
  public async Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<Account>()
      .AnyAsync(account => account.NormalizedCode == normalizedCode, cancellationToken);
  }

  public async Task<IReadOnlyList<Account>> GetManyAsync(
    IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(accountIds);

    if (accountIds.Count == 0)
    {
      return [];
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    var distinct = accountIds.Distinct().ToArray();

    return await context.Set<Account>()
      .Where(account => distinct.Contains(account.Id))
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<Account>().AddAsync(account, cancellationToken);
  }
}

internal sealed class FiscalCalendarRepository(ITenantDbContextAccessor contextAccessor) : IFiscalCalendarRepository
{
  public async Task<FiscalYear?> GetByIdAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<FiscalYear>()
      .Include(year => year.Periods)
      .FirstOrDefaultAsync(year => year.Id == fiscalYearId, cancellationToken);
  }

  // ---- THE PERIODS ARE INCLUDED, ALWAYS.
  //
  // A year without its periods cannot answer `ResolveOpenPeriodFor`, and lazy loading is not configured —
  // so an omitted Include would surface as an empty period collection and a `Gl.FiscalPeriodNotFound` for a
  // date the calendar plainly covers. Loading them is not an optimisation choice here; it is the difference
  // between a correct answer and a confidently wrong one.
  public async Task<FiscalYear?> GetCoveringAsync(
    Guid companyId, DateTimeOffset instantUtc, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    var instant = instantUtc.ToUniversalTime();

    return await context.Set<FiscalYear>()
      .Include(year => year.Periods)
      .FirstOrDefaultAsync(
        year => year.CompanyId == companyId && year.StartUtc <= instant && year.EndUtc > instant,
        cancellationToken);
  }

  public async Task<FiscalPeriod?> GetPeriodAsync(
    Guid fiscalPeriodId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<FiscalPeriod>()
      .FirstOrDefaultAsync(period => period.Id == fiscalPeriodId, cancellationToken);
  }

  public async Task<bool> CodeExistsAsync(
    Guid companyId, string code, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<FiscalYear>()
      .AnyAsync(year => year.CompanyId == companyId && year.Code == code, cancellationToken);
  }

  // Half-open intervals overlap exactly when each starts before the other ends. Written that way rather
  // than as four comparisons because the two-comparison form cannot be got subtly wrong at the boundaries.
  public async Task<bool> OverlapsExistingAsync(
    Guid companyId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    var start = startUtc.ToUniversalTime();
    var end = endUtc.ToUniversalTime();

    return await context.Set<FiscalYear>()
      .AnyAsync(
        year => year.CompanyId == companyId && year.StartUtc < end && start < year.EndUtc,
        cancellationToken);
  }

  public async Task AddAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<FiscalYear>().AddAsync(fiscalYear, cancellationToken);
  }
}

internal sealed class JournalDraftRepository(ITenantDbContextAccessor contextAccessor) : IJournalDraftRepository
{
  public async Task<JournalDraft?> GetByIdAsync(
    Guid journalDraftId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JournalDraft>()
      .Include(draft => draft.Lines)
      .FirstOrDefaultAsync(draft => draft.Id == journalDraftId, cancellationToken);
  }

  public async Task AddAsync(JournalDraft draft, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<JournalDraft>().AddAsync(draft, cancellationToken);
  }

  // See the port: the platform sets every foreign key to `Restrict` AFTER the module contributors run, so
  // `JournalDraftConfiguration`'s configured cascade never takes effect and an orphaned line is a row
  // nothing deletes.
  //
  // Marked Deleted BEFORE `ReplaceLines` clears the collection — afterwards, EF's navigation fixer has
  // already seen the severance and already tried to null a non-nullable foreign key.
  public async Task RemoveLinesAsync(JournalDraft draft, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(draft);

    if (draft.Lines.Count == 0)
    {
      return;
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Materialized before RemoveRange: the navigation and the tracker hold the same objects, and removing
    // from one while enumerating the other is how this becomes intermittent rather than fixed.
    context.Set<JournalDraftLine>().RemoveRange([.. draft.Lines]);
  }

  // Synchronous because it only marks the tracked graph. The context is resolved by the caller's other
  // operations in the same unit of work.
  //
  // ---- THE LINES ARE REMOVED EXPLICITLY, AND THIS COMMENT USED TO SAY OTHERWISE.
  //
  // It previously read "the cascade configured on the draft's lines removes them with it". That cascade is
  // configured and then overwritten: `PersistenceDbContext.OnModelCreating` sets every foreign key in the
  // composed model to `Restrict` after the contributors run. **Discarding a draft that had lines would
  // therefore have failed**, on a delete the module believed the database would handle for it.
  //
  // The comment described the intent; the model described the truth; nothing tested which one shipped.
  public void Remove(JournalDraft draft)
  {
    ArgumentNullException.ThrowIfNull(draft);

    var context = contextAccessor.GetRequiredAsync().GetAwaiter().GetResult();

    if (draft.Lines.Count > 0)
    {
      context.Set<JournalDraftLine>().RemoveRange([.. draft.Lines]);
    }

    context.Set<JournalDraft>().Remove(draft);
  }
}

internal sealed class JournalEntryRepository(ITenantDbContextAccessor contextAccessor) : IJournalEntryRepository
{
  public async Task<JournalEntry?> GetByIdAsync(
    Guid journalEntryId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JournalEntry>()
      .Include(entry => entry.Lines)
      .FirstOrDefaultAsync(entry => entry.Id == journalEntryId, cancellationToken);
  }

  public async Task<bool> ReversalExistsAsync(
    Guid originalJournalEntryId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JournalEntry>()
      .AnyAsync(entry => entry.ReversesJournalEntryId == originalJournalEntryId, cancellationToken);
  }

  // ---- MAX + 1, AND THE RACE IS LOST AT THE INDEX RATHER THAN WON HERE.
  //
  // Two concurrent posts can read the same maximum. Neither a lock nor a sequence is used: the unique index
  // on (TenantId, CompanyId, FiscalYearId, JournalNumber) makes the second writer fail at commit, which is
  // the outcome `AC-GL-0013` asks for and is strictly safer than a number handed out before the row exists.
  //
  // The numbers are stored as text so they sort and display consistently, and parsed back here. That is why
  // this reads every number for the year rather than using SQL MAX — a lexical maximum over "9" and "10"
  // would answer "9". At realistic per-year volumes this is one indexed range read; if it ever stops being
  // cheap, the fix is a numeric column beside the text one, not a lexical shortcut.
  public async Task<int> NextJournalNumberAsync(
    Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var numbers = await context.Set<JournalEntry>()
      .Where(entry => entry.CompanyId == companyId && entry.FiscalYearId == fiscalYearId)
      .Select(entry => entry.JournalNumber)
      .ToListAsync(cancellationToken);

    var highest = 0;
    foreach (var number in numbers)
    {
      if (int.TryParse(number, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > highest)
      {
        highest = parsed;
      }
    }

    return highest + 1;
  }

  public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<JournalEntry>().AddAsync(entry, cancellationToken);
  }
}
