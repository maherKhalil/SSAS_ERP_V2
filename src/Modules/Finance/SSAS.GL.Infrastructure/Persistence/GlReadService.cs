using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Infrastructure.Persistence;

// GL'S READ SIDE.
//
// ---- TWO PREDICATES APPEAR ON EVERY QUERY, AND THEY ARE NOT THE SAME PREDICATE.
//
// `scope.TenantId` is stated explicitly even though the context's global filter already applies it. That is
// the `EmployeeReadScope` convention and the reason is worth repeating: a query should STATE the invariant
// it depends on rather than inherit it silently, so a future change to the filter cannot quietly widen a
// read that looked correct.
//
// `scope.CompanyIds` is the authorized set, materialized. "All companies" is a LIST, never the absence of a
// condition — and the list can never be empty, because `GlReadScope` refuses to exist with an empty one.
//
// ---- SEARCH FILTERS ON THE NORMALIZED COLUMN, NEVER ON THE VALUE-CONVERTED ONE.
//
// `DEC-POS-0030`: a value-converted property translates in a PROJECTION but not in a PREDICATE. Filtering
// on `Code.Value.Contains(...)` throws at runtime for every request that supplies a search term, and HR
// shipped exactly that defect once. `NormalizedCode` and `NormalizedName` exist so this file cannot repeat
// it.
internal sealed class GlReadService(ITenantDbContextAccessor contextAccessor) : IGlReadService
{
  public async Task<IReadOnlyList<AccountListItem>> SearchAccountsAsync(
    GlReadScope scope, string? searchText, bool? isActive, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The chart is TENANT-level (`OD-GL-0003`), so this read carries NO company predicate — every company
    // in the tenant sees the same chart. The scope is still required, because holding one is what proves
    // the caller may read at all.
    var query = context.Set<Account>().AsNoTracking()
      .Where(account => account.TenantId == scope.TenantId);

    if (isActive is { } activeFilter)
    {
      query = query.Where(account => account.IsActive == activeFilter);
    }

    if (!string.IsNullOrWhiteSpace(searchText))
    {
      var pattern = Normalize(searchText);
      query = query.Where(account =>
        account.NormalizedCode.Contains(pattern) || account.NormalizedName.Contains(pattern));
    }

    return await query
      .OrderBy(account => account.NormalizedCode)
      .Select(account => new AccountListItem(
        account.Id, account.Code.Value, account.Name.Value, account.IsActive))
      .ToListAsync(cancellationToken);
  }

  public async Task<AccountListItem?> GetAccountAsync(
    GlReadScope scope, Guid accountId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<Account>().AsNoTracking()
      .Where(account => account.TenantId == scope.TenantId && account.Id == accountId)
      .Select(account => new AccountListItem(
        account.Id, account.Code.Value, account.Name.Value, account.IsActive))
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<FiscalPeriodListItem>> GetFiscalPeriodsAsync(
    GlReadScope scope, Guid? companyId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var years = context.Set<FiscalYear>().AsNoTracking()
      .Where(year => year.TenantId == scope.TenantId && scope.CompanyIds.Contains(year.CompanyId));

    // A caller-supplied company NARROWS the authorized set; it never replaces it. Intersecting rather than
    // substituting is what stops a caller reaching a company by naming it.
    if (companyId is { } requested)
    {
      years = years.Where(year => year.CompanyId == requested);
    }

    return await years
      .SelectMany(year => year.Periods, (year, period) => new FiscalPeriodListItem(
        period.Id,
        year.Id,
        year.Code,
        period.Name,
        period.StartUtc,
        period.EndUtc,
        period.Status == FiscalPeriodStatus.Open))
      .OrderBy(period => period.StartUtc)
      .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<JournalListItem>> SearchJournalsAsync(
    GlReadScope scope,
    Guid? companyId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    string? reference,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var query = context.Set<JournalEntry>().AsNoTracking()
      .Where(entry => entry.TenantId == scope.TenantId && scope.CompanyIds.Contains(entry.CompanyId));

    if (companyId is { } requested)
    {
      query = query.Where(entry => entry.CompanyId == requested);
    }

    if (fromUtc is { } from)
    {
      query = query.Where(entry => entry.EntryDateUtc >= from);
    }

    if (toUtc is { } to)
    {
      query = query.Where(entry => entry.EntryDateUtc < to);
    }

    if (!string.IsNullOrWhiteSpace(reference))
    {
      var trimmed = reference.Trim();
      query = query.Where(entry => entry.Reference == trimmed);
    }

    var reversals = context.Set<JournalEntry>().AsNoTracking()
      .Where(candidate => candidate.TenantId == scope.TenantId);

    return await query
      .OrderByDescending(entry => entry.EntryDateUtc)
      .ThenBy(entry => entry.JournalNumber)
      .Select(entry => new JournalListItem(
        entry.Id,
        entry.CompanyId,
        entry.JournalNumber,
        entry.EntryDateUtc,
        entry.Description,
        entry.Reference,
        entry.Lines.Sum(line => line.Debit),
        entry.ReversesJournalEntryId,
        reversals.Any(candidate => candidate.ReversesJournalEntryId == entry.Id)))
      .ToListAsync(cancellationToken);
  }

  public async Task<JournalDetail?> GetJournalAsync(
    GlReadScope scope, Guid journalEntryId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var accounts = context.Set<Account>().AsNoTracking();
    var reversals = context.Set<JournalEntry>().AsNoTracking()
      .Where(candidate => candidate.TenantId == scope.TenantId);

    return await context.Set<JournalEntry>().AsNoTracking()
      .Where(entry => entry.TenantId == scope.TenantId
        && scope.CompanyIds.Contains(entry.CompanyId)
        && entry.Id == journalEntryId)
      .Select(entry => new JournalDetail(
        entry.Id,
        entry.CompanyId,
        entry.JournalNumber,
        entry.EntryDateUtc,
        entry.Description,
        entry.Reference,
        entry.ReversesJournalEntryId,
        reversals.Any(candidate => candidate.ReversesJournalEntryId == entry.Id),
        entry.Lines
          .OrderBy(line => line.LineNumber)
          .Select(line => new JournalLineDetail(
            line.LineNumber,
            line.AccountId,
            accounts.Where(account => account.Id == line.AccountId).Select(account => account.Code.Value).First(),
            accounts.Where(account => account.Id == line.AccountId).Select(account => account.Name.Value).First(),
            line.Debit,
            line.Credit,
            line.Description))
          .ToList()))
      .FirstOrDefaultAsync(cancellationToken);
  }

  // ================================================================================================
  // THE DRAFT READS (T-098). SAME PREDICATES AS THE JOURNAL READS, AND THAT IS THE POINT.
  // ================================================================================================
  //
  // Tenant and company come from the scope, never from the caller, exactly as `SearchJournalsAsync` does.
  // A draft is scratch space rather than a ledger entry, **and that changes nothing about who may see it**:
  // it belongs to a company and is readable only by someone the scope admits to that company.
  public async Task<IReadOnlyList<JournalDraftListItem>> SearchJournalDraftsAsync(
    GlReadScope scope,
    Guid? companyId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    string? reference,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var query = context.Set<JournalDraft>().AsNoTracking()
      .Where(draft => draft.TenantId == scope.TenantId && scope.CompanyIds.Contains(draft.CompanyId));

    if (companyId is { } requested)
    {
      query = query.Where(draft => draft.CompanyId == requested);
    }

    if (fromUtc is { } from)
    {
      query = query.Where(draft => draft.EntryDateUtc >= from);
    }

    if (toUtc is { } to)
    {
      query = query.Where(draft => draft.EntryDateUtc < to);
    }

    if (!string.IsNullOrWhiteSpace(reference))
    {
      var trimmed = reference.Trim();
      query = query.Where(draft => draft.Reference == trimmed);
    }

    // ---- ORDERED BY DATE THEN ID, NOT BY NUMBER.
    //
    // `SearchJournalsAsync` breaks ties on `JournalNumber`. **A draft has no number** — it is assigned at
    // posting — so the id is the only stable tiebreak, and a stable one is required or two calls can
    // return the same rows in a different order.
    return await query
      .OrderByDescending(draft => draft.EntryDateUtc)
      .ThenBy(draft => draft.Id)
      .Select(draft => new JournalDraftListItem(
        draft.Id,
        draft.CompanyId,
        draft.EntryDateUtc,
        draft.Description,
        draft.Reference,
        draft.Lines.Sum(line => line.Debit)))
      .ToListAsync(cancellationToken);
  }

  public async Task<JournalDraftDetail?> GetJournalDraftAsync(
    GlReadScope scope, Guid journalDraftId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var accounts = context.Set<Account>().AsNoTracking();

    return await context.Set<JournalDraft>().AsNoTracking()
      .Where(draft => draft.TenantId == scope.TenantId
        && scope.CompanyIds.Contains(draft.CompanyId)
        && draft.Id == journalDraftId)
      .Select(draft => new JournalDraftDetail(
        draft.Id,
        draft.CompanyId,
        draft.EntryDateUtc,
        draft.Description,
        draft.Reference,
        draft.Lines
          .OrderBy(line => line.LineNumber)
          .Select(line => new JournalLineDetail(
            line.LineNumber,
            line.AccountId,
            accounts.Where(account => account.Id == line.AccountId).Select(account => account.Code.Value).First(),
            accounts.Where(account => account.Id == line.AccountId).Select(account => account.Name.Value).First(),
            line.Debit,
            line.Credit,
            line.Description))
          .ToList()))
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<AccountBalance?> GetAccountBalanceAsync(
    GlReadScope scope,
    Guid accountId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var account = await context.Set<Account>().AsNoTracking()
      .FirstOrDefaultAsync(
        candidate => candidate.TenantId == scope.TenantId && candidate.Id == accountId, cancellationToken);

    if (account is null)
    {
      return null;
    }

    // ---- THE SCOPE PREDICATE IS APPLIED TO THE MOVEMENTS, NOT ONLY TO THE ACCOUNT.
    //
    // The chart is tenant-wide, so an account is visible to every caller — but its MOVEMENTS belong to
    // companies, and a caller must not see totals from a company they cannot reach. Filtering the account
    // and forgetting the lines would produce a plausible number that silently includes another company's
    // postings, which is the defect `TS-GL-0032` describes for the trial balance.
    var lines = context.Set<JournalLine>().AsNoTracking()
      .Where(line => line.TenantId == scope.TenantId && line.AccountId == accountId);

    var entries = context.Set<JournalEntry>().AsNoTracking()
      .Where(entry => entry.TenantId == scope.TenantId && scope.CompanyIds.Contains(entry.CompanyId));

    if (fromUtc is { } from)
    {
      entries = entries.Where(entry => entry.EntryDateUtc >= from);
    }

    if (toUtc is { } to)
    {
      entries = entries.Where(entry => entry.EntryDateUtc < to);
    }

    var scoped = lines.Where(line => entries.Any(entry => entry.Id == line.JournalEntryId));

    var debits = await scoped.SumAsync(line => (decimal?)line.Debit, cancellationToken) ?? 0m;
    var credits = await scoped.SumAsync(line => (decimal?)line.Credit, cancellationToken) ?? 0m;

    return new AccountBalance(account.Id, account.Code.Value, account.Name.Value, debits, credits);
  }

  public async Task<TrialBalance> GetTrialBalanceAsync(
    GlReadScope scope,
    Guid companyId,
    DateTimeOffset fromUtc,
    DateTimeOffset toUtc,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- ONE PREDICATE, BUILT ONCE, APPLIED TO BOTH SIDES.
    //
    // The trial balance's whole claim is that debits equal credits. A filter applied to one side and not
    // the other produces a report that looks right and is silently wrong — so the two sums are computed
    // from the SAME filtered set in one grouping rather than from two queries that could drift apart.
    var entries = context.Set<JournalEntry>().AsNoTracking()
      .Where(entry => entry.TenantId == scope.TenantId
        && scope.CompanyIds.Contains(entry.CompanyId)
        && entry.CompanyId == companyId
        && entry.EntryDateUtc >= fromUtc
        && entry.EntryDateUtc < toUtc);

    var rows = await context.Set<JournalLine>().AsNoTracking()
      .Where(line => line.TenantId == scope.TenantId
        && entries.Any(entry => entry.Id == line.JournalEntryId))
      .GroupBy(line => line.AccountId)
      .Select(group => new
      {
        AccountId = group.Key,
        TotalDebits = group.Sum(line => line.Debit),
        TotalCredits = group.Sum(line => line.Credit)
      })
      .Join(
        context.Set<Account>().AsNoTracking(),
        row => row.AccountId,
        account => account.Id,
        (row, account) => new TrialBalanceRow(
          account.Id, account.Code.Value, account.Name.Value, row.TotalDebits, row.TotalCredits))
      .OrderBy(row => row.Code)
      .ToListAsync(cancellationToken);

    return new TrialBalance(rows);
  }

  // Matches the normalization the value objects apply, so a search term and a stored code are compared on
  // the same footing. Ordinal upper-casing, no Unicode normalization — the same rule `AccountCode` states.
  private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
