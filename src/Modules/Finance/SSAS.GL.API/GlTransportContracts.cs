namespace SSAS.GL.API;

// GL'S WIRE SHAPES (api-contracts.md).
//
// ================================================================================================
// NOTHING HERE CARRIES A CURRENCY, AND NOTHING HERE CARRIES A FISCAL PERIOD.
// ================================================================================================
//
// **No currency** because `OD-GL-0002` ruled V1 single-currency and `ADR-027` decision 2 projects the
// company's `BaseCurrencyCode` on READ. A request that supplies one is refused by the strict reader as an
// unknown property — which is the reader doing its ordinary job, not a special case (`TS-GL-0027`).
//
// **No fiscal period** because it is RESOLVED from the entry date (`AC-GL-0002`). A caller who could name
// the period could post into one the date does not belong to, which would make `BR-GL-0003` unenforceable
// by inspection: the closed-period check would be guarding a field the caller chose. There is no property
// for it, so the request cannot express the idea.
//
// **No branch** because `OD-GL-0005` declined the dimension for V1.

// ---- ACCOUNTS.
//
// No `code` on the update request. `REQ-GL-0006` makes the code immutable from creation, and the aggregate
// has no method to change it — so the wire shape has no field for it either. A caller who sends one gets a
// 400 from the strict reader rather than a silently ignored property.
public sealed record CreateAccountRequest(string Code, string Name);

public sealed record UpdateAccountRequest(string Name, string? RowVersion);

public sealed record AccountActivationRequest(string? RowVersion);

public sealed record AccountResponse(Guid AccountId, string Code, string Name, bool IsActive);

// ---- FISCAL CALENDAR.
//
// The periods are supplied WITH the year and cannot be added afterwards: contiguity is a property of the
// SET, and `FiscalYear.Create` validates the whole partition in one call. A shape that allowed adding a
// period later would allow a calendar to exist in an invalid state between two requests.
public sealed record DefineFiscalYearRequest(
  string Code,
  DateTimeOffset StartUtc,
  DateTimeOffset EndUtc,
  IReadOnlyList<FiscalPeriodRequest> Periods);

public sealed record FiscalPeriodRequest(string Name, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed record FiscalPeriodStateRequest(string? RowVersion);

public sealed record FiscalPeriodResponse(
  Guid FiscalPeriodId,
  Guid FiscalYearId,
  string FiscalYearCode,
  string Name,
  DateTimeOffset StartUtc,
  DateTimeOffset EndUtc,
  bool IsOpen);

// ---- DRAFTS.
//
// `debit` and `credit` are separate fields rather than one signed amount, matching the storage decision and
// keeping the wire honest about which side a line is on. A line carrying both, or neither, or a negative
// amount is refused — those are malformed rather than merely unbalanced, so they fail at 400 rather than
// 422.
public sealed record JournalLineRequest(Guid AccountId, decimal Debit, decimal Credit, string? Description);

public sealed record CreateJournalDraftRequest(
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  IReadOnlyList<JournalLineRequest> Lines);

public sealed record UpdateJournalDraftRequest(
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  IReadOnlyList<JournalLineRequest> Lines,
  string? RowVersion);

// ---- POSTING AND REVERSAL.
//
// Posting takes NO body: everything it needs is on the draft it names, and a body would let a caller change
// what is posted at the moment of posting — which is precisely the thing the draft/entry split exists to
// make impossible.
public sealed record ReverseJournalRequest(DateTimeOffset ReversalDateUtc, string Description);

// ---- RESPONSES.
//
// `currencyCode` is PROJECTED here from the owning company, never stored and never accepted. It is present
// because an amount displayed without a currency is unreadable — `ADR-027` decision 2 says exactly this.
public sealed record JournalLineResponse(
  int LineNumber,
  Guid AccountId,
  string AccountCode,
  string AccountName,
  decimal Debit,
  decimal Credit,
  string? Description);

public sealed record JournalResponse(
  Guid JournalEntryId,
  Guid CompanyId,
  string JournalNumber,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  string CurrencyCode,
  Guid? ReversesJournalEntryId,
  bool IsReversed,
  IReadOnlyList<JournalLineResponse> Lines);

public sealed record JournalSummaryResponse(
  Guid JournalEntryId,
  Guid CompanyId,
  string JournalNumber,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  string CurrencyCode,
  decimal Total,
  Guid? ReversesJournalEntryId,
  bool IsReversed);

// Both totals travel, not merely the net. A single signed number cannot distinguish "no movement" from
// "equal movement both ways", and `AC-GL-0015` asserts the balance equals the sum of what was returned —
// which is only checkable if both sides are present.
public sealed record AccountBalanceResponse(
  Guid AccountId,
  string Code,
  string Name,
  string CurrencyCode,
  decimal TotalDebits,
  decimal TotalCredits,
  decimal Balance);

public sealed record TrialBalanceRowResponse(
  Guid AccountId, string Code, string Name, decimal TotalDebits, decimal TotalCredits);

// `balances` is computed from the rows that were returned, so a client can assert the report's own claim
// without re-summing. A trial balance that does not balance is the symptom of a scope predicate applied to
// one side of the ledger and not the other (`TS-GL-0032`).
public sealed record TrialBalanceResponse(
  Guid CompanyId,
  DateTimeOffset FromUtc,
  DateTimeOffset ToUtc,
  string CurrencyCode,
  decimal TotalDebits,
  decimal TotalCredits,
  bool Balances,
  IReadOnlyList<TrialBalanceRowResponse> Rows);
