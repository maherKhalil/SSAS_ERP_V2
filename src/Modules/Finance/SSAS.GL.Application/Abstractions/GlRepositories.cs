using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Application.Abstractions;

// GL'S WRITE-SIDE PORTS.
//
// One interface per aggregate root, each exposing only the operations its handlers need. The absences are
// as deliberate as the presences and are noted where a reader might expect a method.
public interface IAccountRepository
{
  Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

  // The chart is TENANT-level (`OD-GL-0003`), so uniqueness is asked without a company. That shorter
  // signature is the ruling made visible: `IDepartmentRepository.CodeExistsAsync` takes a `companyId`
  // because departments are company-owned, and this one does not because accounts are not.
  Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default);

  // Posting needs every named account in ONE round trip, not N. A journal with forty lines would otherwise
  // issue forty queries inside the writing transaction, holding it open for the duration.
  Task<IReadOnlyList<Account>> GetManyAsync(
    IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);

  Task AddAsync(Account account, CancellationToken cancellationToken = default);

  // No Remove. `BR-GL-0004` makes deactivation the lifecycle, and an account is never deleted — so the
  // repository offers no way to, and `TenantDbContext.PreventCompanyDeletion`'s reasoning applies: history
  // stays reconstructable.
}

public interface IFiscalCalendarRepository
{
  Task<FiscalYear?> GetByIdAsync(Guid fiscalYearId, CancellationToken cancellationToken = default);

  // Loaded WITH its periods, because the period is what a posting resolves and a year without them cannot
  // answer `ResolveOpenPeriodFor`.
  Task<FiscalYear?> GetCoveringAsync(
    Guid companyId, DateTimeOffset instantUtc, CancellationToken cancellationToken = default);

  Task<FiscalPeriod?> GetPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsAsync(
    Guid companyId, string code, CancellationToken cancellationToken = default);

  // A company's years must not overlap, or an entry date would resolve to two calendars.
  Task<bool> OverlapsExistingAsync(
    Guid companyId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default);

  Task AddAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default);
}

public interface IJournalDraftRepository
{
  Task<JournalDraft?> GetByIdAsync(Guid journalDraftId, CancellationToken cancellationToken = default);

  Task AddAsync(JournalDraft draft, CancellationToken cancellationToken = default);

  // Drafts ARE deletable, and they are the only deletable thing in this module. That is not an
  // inconsistency with `BR-GL-0002`: a draft is scratch space that was never part of the ledger, and
  // `OD-GL-0007` chose two aggregates precisely so discarding one could be an ordinary delete rather than a
  // hole in the append-only guarantee.
  void Remove(JournalDraft draft);

  // ================================================================================================
  // REPLACING A DRAFT'S LINES DELETES THE OLD ONES EXPLICITLY (FP-013 follow-up).
  // ================================================================================================
  //
  // The same defect FP-013's chain test found in Payroll, in the same shape here. `JournalDraft.ReplaceLines`
  // does `lines.Clear()`, and `JournalDraftConfiguration` asks for `DeleteBehavior.Cascade` and does not get
  // it: `PersistenceDbContext.OnModelCreating` sets EVERY foreign key in the composed model to `Restrict`
  // AFTER the module contributors run. Deliberate platform policy — no silent cascades in a multi-tenant
  // model — and `TenantDbContext` names it where the contributors are applied.
  //
  // So updating a draft that already HAS lines orphans rows nothing deletes, against a non-nullable foreign
  // key EF cannot null, and the save fails.
  //
  // **This was never observed because GL's update path has never been driven against real SQL through its
  // real handler** — the same blind spot that hid Payroll's, found by looking rather than by failing.
  // `GlJournalPoster` is unaffected: the draft it builds is transient and never tracked, so it has no old
  // lines to orphan.
  Task RemoveLinesAsync(JournalDraft draft, CancellationToken cancellationToken = default);
}

public interface IJournalEntryRepository
{
  Task<JournalEntry?> GetByIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default);

  // Answers "was this reversed?" — a fact DERIVED from the existence of a reversal pointing at the
  // original, never a status column on it. Storing that status would require modifying an append-only row,
  // which the write boundary refuses; this query is what the design pays instead, and the cost is one
  // indexed lookup.
  Task<bool> ReversalExistsAsync(Guid originalJournalEntryId, CancellationToken cancellationToken = default);

  // The next journal number within (CompanyId, FiscalYear) — `BR-GL-0005` as scoped by `OD-GL-0004`.
  //
  // UNIQUE, NOT GAPLESS. This returns max + 1, so a failed post consumes nothing but a concurrent one can
  // race — and loses at the unique index rather than silently duplicating. `AC-GL-0013` asserts uniqueness
  // only, and gaplessness was raised and deliberately not promised.
  Task<int> NextJournalNumberAsync(
    Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default);

  Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default);

  // No Update and no Remove, and their absence is the courtesy rather than the guarantee. The guarantee is
  // `TenantDbContext.PreventAppendOnlyMutation`, which refuses a Modified or Deleted entry for the type by
  // whatever path tracked it — including a caller who bypasses this interface entirely.
}
