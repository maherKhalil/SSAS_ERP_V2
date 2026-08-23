namespace SSAS.GL.Application.Reads;

// THE READ PORT. EVERY METHOD TAKES A `GlReadScope`, AND NONE HAS AN OVERLOAD WITHOUT ONE.
//
// That is the enforcement, not a convention: the type cannot be constructed outside `GlScopeResolver`, and
// there is no way to call any of these without holding one. A read that forgot its scope predicate is
// unwritable rather than merely reviewable (`DEC-GL-0004`, `AC-GL-0014`).
//
// The scope is the FIRST parameter everywhere, so a call site missing it fails to compile at the opening
// paren rather than deep in an argument list.
public interface IGlReadService
{
  Task<IReadOnlyList<AccountListItem>> SearchAccountsAsync(
    GlReadScope scope, string? searchText, bool? isActive, CancellationToken cancellationToken = default);

  Task<AccountListItem?> GetAccountAsync(
    GlReadScope scope, Guid accountId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<FiscalPeriodListItem>> GetFiscalPeriodsAsync(
    GlReadScope scope, Guid? companyId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<JournalListItem>> SearchJournalsAsync(
    GlReadScope scope,
    Guid? companyId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    string? reference,
    CancellationToken cancellationToken = default);

  Task<JournalDetail?> GetJournalAsync(
    GlReadScope scope, Guid journalEntryId, CancellationToken cancellationToken = default);

  Task<AccountBalance?> GetAccountBalanceAsync(
    GlReadScope scope,
    Guid accountId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    CancellationToken cancellationToken = default);

  Task<TrialBalance> GetTrialBalanceAsync(
    GlReadScope scope,
    Guid companyId,
    DateTimeOffset fromUtc,
    DateTimeOffset toUtc,
    CancellationToken cancellationToken = default);
}
