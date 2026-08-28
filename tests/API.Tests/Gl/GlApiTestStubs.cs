using SSAS.BuildingBlocks.Domain;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.API.Tests.Gl;

// GL'S API-LAYER STUBS.
//
// The API tests prove TRANSPORT: routing, permission enforcement, strict reading, status codes, error
// codes, and scope bleed. They deliberately prove nothing about persistence — these stubs hold objects in
// memory and every one of them succeeds unless a test tells it not to.
//
// What that means, stated so nobody mistakes a green API suite for coverage it does not provide: the
// append-only guarantee, the posting transaction, the concurrency token and the fiscal-period race are all
// enforced below this layer and are asserted in `Integration.Tests` against real SQL. FP-009 recorded the
// cost of forgetting that — an API test asserted a rollback property its harness could not exercise,
// because the harness's transaction was a no-op.

public sealed class StubGlReads : IGlReadService
{
  public List<AccountListItem> Accounts { get; } = [];

  public List<FiscalPeriodListItem> Periods { get; } = [];

  public List<JournalListItem> Journals { get; } = [];

  public JournalDetail? Journal { get; set; }

  // T-098's draft reads. Separate collections rather than reusing the journal ones: a test that set
  // `Journals` and asserted a DRAFT route returned them would be asserting the stub, not the route.
  public List<JournalDraftListItem> Drafts { get; } = [];

  public JournalDraftDetail? Draft { get; set; }

  public AccountBalance? Balance { get; set; }

  public TrialBalance TrialBalance { get; set; } = new([]);

  // Records the scope every read was called with, so a test can assert the route actually passed one and
  // that it carried the companies the caller was authorized for rather than the ones they asked for.
  public List<GlReadScope> ObservedScopes { get; } = [];

  public void Reset()
  {
    Accounts.Clear();
    Periods.Clear();
    Journals.Clear();
    ObservedScopes.Clear();
    Journal = null;
    Balance = null;
    TrialBalance = new TrialBalance([]);
  }

  public Task<IReadOnlyList<AccountListItem>> SearchAccountsAsync(
    GlReadScope scope, string? searchText, bool? isActive, CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult<IReadOnlyList<AccountListItem>>(Accounts);
  }

  public Task<AccountListItem?> GetAccountAsync(
    GlReadScope scope, Guid accountId, CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult(Accounts.FirstOrDefault(account => account.AccountId == accountId));
  }

  public Task<IReadOnlyList<FiscalPeriodListItem>> GetFiscalPeriodsAsync(
    GlReadScope scope, Guid? companyId, CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult<IReadOnlyList<FiscalPeriodListItem>>(Periods);
  }

  public Task<IReadOnlyList<JournalListItem>> SearchJournalsAsync(
    GlReadScope scope, Guid? companyId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? reference,
    CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult<IReadOnlyList<JournalListItem>>(Journals);
  }

  public Task<JournalDetail?> GetJournalAsync(
    GlReadScope scope, Guid journalEntryId, CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult(Journal?.JournalEntryId == journalEntryId ? Journal : null);
  }

  public Task<IReadOnlyList<JournalDraftListItem>> SearchJournalDraftsAsync(
    GlReadScope scope, Guid? companyId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? reference,
    CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult<IReadOnlyList<JournalDraftListItem>>(Drafts);
  }

  public Task<JournalDraftDetail?> GetJournalDraftAsync(
    GlReadScope scope, Guid journalDraftId, CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult(Draft?.JournalDraftId == journalDraftId ? Draft : null);
  }

  public Task<AccountBalance?> GetAccountBalanceAsync(
    GlReadScope scope, Guid accountId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc,
    CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult(Balance);
  }

  public Task<TrialBalance> GetTrialBalanceAsync(
    GlReadScope scope, Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc,
    CancellationToken cancellationToken = default)
  {
    ObservedScopes.Add(scope);
    return Task.FromResult(TrialBalance);
  }
}

public sealed class StubAccountRepository : IAccountRepository
{
  public Dictionary<Guid, Account> Accounts { get; } = [];

  public bool CodeTaken { get; set; }

  public List<Account> Added { get; } = [];

  public void Reset()
  {
    Accounts.Clear();
    Added.Clear();
    CodeTaken = false;
  }

  public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Accounts.TryGetValue(accountId, out var account) ? account : null);

  public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<IReadOnlyList<Account>> GetManyAsync(
    IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<Account>>(
      [.. accountIds.Where(Accounts.ContainsKey).Select(id => Accounts[id])]);

  public Task AddAsync(Account account, CancellationToken cancellationToken = default)
  {
    Added.Add(account);
    Accounts[account.Id] = account;
    return Task.CompletedTask;
  }
}

public sealed class StubCalendarRepository : IFiscalCalendarRepository
{
  public Dictionary<Guid, FiscalYear> Years { get; } = [];

  public bool CodeTaken { get; set; }

  public bool Overlaps { get; set; }

  public List<FiscalYear> Added { get; } = [];

  public void Reset()
  {
    Years.Clear();
    Added.Clear();
    CodeTaken = false;
    Overlaps = false;
  }

  public Task<FiscalYear?> GetByIdAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Years.TryGetValue(fiscalYearId, out var year) ? year : null);

  public Task<FiscalYear?> GetCoveringAsync(
    Guid companyId, DateTimeOffset instantUtc, CancellationToken cancellationToken = default) =>
    Task.FromResult(Years.Values.FirstOrDefault(
      year => year.CompanyId == companyId && year.Covers(instantUtc)));

  public Task<FiscalPeriod?> GetPeriodAsync(
    Guid fiscalPeriodId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Years.Values
      .SelectMany(year => year.Periods)
      .FirstOrDefault(period => period.Id == fiscalPeriodId));

  public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<bool> OverlapsExistingAsync(
    Guid companyId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default) =>
    Task.FromResult(Overlaps);

  public Task AddAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
  {
    Added.Add(fiscalYear);
    Years[fiscalYear.Id] = fiscalYear;
    return Task.CompletedTask;
  }
}

public sealed class StubJournalDraftRepository : IJournalDraftRepository
{
  // ---- NOTHING TO DO, AND THE EMPTINESS IS THE POINT.
  //
  // An in-memory stub has no change tracker and therefore no orphans. The defect this method exists for is
  // a PERSISTENCE fact — the platform overriding a module's configured cascade with `Restrict` — and it is
  // invisible to every stub by construction. That is precisely why it survived until a real-SQL end-to-end
  // test drove the equivalent Payroll path.
  public Task RemoveLinesAsync(JournalDraft draft, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;

  public Dictionary<Guid, JournalDraft> Drafts { get; } = [];

  public List<JournalDraft> Added { get; } = [];

  public List<JournalDraft> Removed { get; } = [];

  public void Reset()
  {
    Drafts.Clear();
    Added.Clear();
    Removed.Clear();
  }

  public Task<JournalDraft?> GetByIdAsync(Guid journalDraftId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Drafts.TryGetValue(journalDraftId, out var draft) ? draft : null);

  public Task AddAsync(JournalDraft draft, CancellationToken cancellationToken = default)
  {
    Added.Add(draft);
    Drafts[draft.Id] = draft;
    return Task.CompletedTask;
  }

  public void Remove(JournalDraft draft)
  {
    Removed.Add(draft);
    Drafts.Remove(draft.Id);
  }
}

public sealed class StubJournalEntryRepository : IJournalEntryRepository
{
  public Dictionary<Guid, JournalEntry> Entries { get; } = [];

  public bool ReversalExists { get; set; }

  public int NextNumber { get; set; } = 1;

  public List<JournalEntry> Added { get; } = [];

  public void Reset()
  {
    Entries.Clear();
    Added.Clear();
    ReversalExists = false;
    NextNumber = 1;
  }

  public Task<JournalEntry?> GetByIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Entries.TryGetValue(journalEntryId, out var entry) ? entry : null);

  public Task<bool> ReversalExistsAsync(
    Guid originalJournalEntryId, CancellationToken cancellationToken = default) =>
    Task.FromResult(ReversalExists);

  public Task<int> NextJournalNumberAsync(
    Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default) =>
    Task.FromResult(NextNumber);

  public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
  {
    Added.Add(entry);
    Entries[entry.Id] = entry;
    return Task.CompletedTask;
  }
}
