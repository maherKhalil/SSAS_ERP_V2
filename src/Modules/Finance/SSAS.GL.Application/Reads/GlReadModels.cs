namespace SSAS.GL.Application.Reads;

// GL'S READ MODELS.
//
// ================================================================================================
// EVERY ONE OF THESE IS REACHED ONLY THROUGH A `GlReadScope`. THERE IS NO OVERLOAD WITHOUT ONE.
// ================================================================================================
//
// `DEC-GL-0004`: a read that omitted its scope predicate is not something a reviewer has to notice, because
// it is not something a caller can express.
//
// ---- NO CURRENCY FIELD ANYWHERE, AND THAT IS `OD-GL-0002` PLUS `ADR-027` DECISION 2.
//
// V1 is single-currency: every amount is denominated in its company's `BaseCurrencyCode`, and no row stores
// one. The currency is PROJECTED on read by the transport layer from `ITenantCompanyCurrencyLookup`, never
// carried here and never accepted on write. If `OD-GL-0002` is ever revisited, these records gain a
// currency and the compiler finds every consumer.

public sealed record AccountListItem(Guid AccountId, string Code, string Name, bool IsActive);

public sealed record FiscalPeriodListItem(
  Guid FiscalPeriodId,
  Guid FiscalYearId,
  string FiscalYearCode,
  string Name,
  DateTimeOffset StartUtc,
  DateTimeOffset EndUtc,
  bool IsOpen);

// `IsReversed` is DERIVED — the existence of a reversal pointing at this entry — never a stored status.
// Storing it would require modifying an append-only row, which the write boundary refuses. The read pays a
// join; the ledger keeps its guarantee.
public sealed record JournalListItem(
  Guid JournalEntryId,
  Guid CompanyId,
  string JournalNumber,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  decimal TotalDebits,
  Guid? ReversesJournalEntryId,
  bool IsReversed);

public sealed record JournalLineDetail(
  int LineNumber,
  Guid AccountId,
  string AccountCode,
  string AccountName,
  decimal Debit,
  decimal Credit,
  string? Description);

// ==================================================================================================
// THE DRAFT READ MODELS (T-098) — THE JOURNAL ONES MINUS WHAT A DRAFT DOES NOT HAVE.
// ==================================================================================================
//
// **No `JournalNumber`:** a number is assigned when a draft is POSTED, so a draft has none. Carrying the
// field as a null would invite a reader to populate it and a caller to render it.
//
// **No `ReversesJournalEntryId`, no `IsReversed`:** reversal is a posted-journal concept — `OD-GL-0007`
// made the draft a distinct aggregate precisely because the two have different lifecycles.
//
// **The LINE model is reused unchanged.** `JournalDraftLine` matches `JournalLine` field for field, so a
// second record would be two names for one shape and they would drift the first time one gained a column.
public sealed record JournalDraftListItem(
  Guid JournalDraftId,
  Guid CompanyId,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  decimal TotalDebits);

public sealed record JournalDraftDetail(
  Guid JournalDraftId,
  Guid CompanyId,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  IReadOnlyList<JournalLineDetail> Lines);

public sealed record JournalDetail(
  Guid JournalEntryId,
  Guid CompanyId,
  string JournalNumber,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  Guid? ReversesJournalEntryId,
  bool IsReversed,
  IReadOnlyList<JournalLineDetail> Lines);

// ---- BALANCE IS DEBITS MINUS CREDITS, AND BOTH TOTALS ARE RETURNED ALONGSIDE IT.
//
// A single signed number cannot distinguish "no movement" from "equal movement in both directions", and an
// accountant checking a balance wants to see the two sides. `AC-GL-0015` asserts the total equals the sum
// of the movements returned, which is only checkable if both are present.
//
// There is NO opening balance: `OD-GL-0008` ruled year-end close out of V1, so there is nothing carried
// forward to add. When that changes, this record gains a field rather than silently changing what `Balance`
// means.
public sealed record AccountBalance(
  Guid AccountId,
  string Code,
  string Name,
  decimal TotalDebits,
  decimal TotalCredits)
{
  public decimal Balance => TotalDebits - TotalCredits;
}

public sealed record TrialBalanceRow(
  Guid AccountId,
  string Code,
  string Name,
  decimal TotalDebits,
  decimal TotalCredits);

// The whole point of the report is that the two totals match (`AC-GL-0016`). They are computed from the
// returned rows rather than queried separately, so a trial balance that does not balance is evidence of a
// filter applied to one side of the ledger and not the other — which is exactly the defect `TS-GL-0032`
// exists to catch, and it could not be caught if the totals came from a different query than the rows.
public sealed record TrialBalance(IReadOnlyList<TrialBalanceRow> Rows)
{
  public decimal TotalDebits => Rows.Sum(row => row.TotalDebits);

  public decimal TotalCredits => Rows.Sum(row => row.TotalCredits);

  public bool Balances => TotalDebits == TotalCredits;
}
