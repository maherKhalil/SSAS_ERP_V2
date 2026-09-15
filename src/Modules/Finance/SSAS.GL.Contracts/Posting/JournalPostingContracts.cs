namespace SSAS.GL.Contracts.Posting;

// ================================================================================================
// THE LEDGER POSTING BOUNDARY. SHAPED BY ITS CONSUMER (OD-PAY-0013).
// ================================================================================================
//
// Every type here exists because PAYROLL needs it. That is the recreate-condition FP-011 recorded when it
// deleted this assembly -- "returns when Payroll consumes it, shaped by its consumer" -- and it is the
// difference between a contract and a guess about what a ledger boundary ought to look like.
//
// ---- WHY THIS ASSEMBLY REFERENCES NOTHING.
//
// `SSAS.HR.Contracts` is reference-free and this follows it. Two consequences are deliberate:
//
//   * The house `Result<T>` (SSAS.BuildingBlocks.Domain) is NOT used here. Referencing it would be legal
//     under `ADR-012` -- a building block is not a module -- but it would bind every consumer of this
//     boundary to the ledger's error model, and `Result<T>` carries an open-ended problem code. A caller
//     would then have to match on strings to find out whether it may retry.
//   * So the outcome below is a CLOSED set of refusals. GL must declare which refusals a poster has to
//     handle, rather than leaving the consumer to discover them from produced errors. `OD-PAY-0014` requires
//     Payroll to refuse a run "naming the period" -- that is only possible if closed-period is a
//     distinguishable outcome rather than one string among many.

// ---- WHAT PAYROLL SENDS.
//
// NOTE WHAT IS ABSENT: there is no fiscal period, and no currency.
//
// **No fiscal period** because GL resolves it from the date. A caller that could name the period could post
// into one the date does not belong to, which would make the closed-period check guard a field the caller
// chose. GL's own HTTP surface refuses a period for exactly this reason, and the principle generalises:
//
//     A CROSS-MODULE CONTRACT HAS NO BUSINESS BEING LAXER THAN THE OWNING MODULE'S OWN HTTP SURFACE.
//     GL DERIVES; CALLERS DATE.
//
// **No currency** because the company's base currency is projected on read and never stored per row
// (`ADR-027` decision 2, `OD-GL-0002`). A payroll denominated in another currency is the multi-currency
// trigger, and it is not pulled here.
public sealed record JournalPostingRequest(
  Guid CompanyId,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  IReadOnlyList<JournalPostingLine> Lines);

// Debit and credit as separate amounts rather than one signed value, matching how the ledger stores them.
// A line carrying both, or neither, is malformed rather than merely unbalanced.
public sealed record JournalPostingLine(
  Guid AccountId,
  decimal Debit,
  decimal Credit,
  string? Description);

// A reversal names the journal it reverses and supplies nothing that could make it differ from what it
// claims to reverse -- GL derives the mirrored lines itself. Payroll's correction path (`OD-PAY-0011`)
// needs this because a posted run can only be corrected by reversal, never by an edit.
public sealed record JournalReversalRequest(
  Guid JournalEntryId,
  DateTimeOffset ReversalDateUtc,
  string Description);

// ---- WHAT GL ANSWERS.
//
// A closed set. Each member is a refusal a POSTER MUST BE ABLE TO ACT ON, which is why the set is small:
// anything Payroll could not act on differently is not worth distinguishing here.
public enum JournalPostingStatus
{
  Posted = 0,

  // The target fiscal period is closed (`BR-GL-0003`). Payroll refuses the run and names the period
  // (`OD-PAY-0014`), so the period's identity travels back with it.
  PeriodClosed = 1,

  // No fiscal period exists covering the date at all -- distinct from closed, because the operator's remedy
  // is different: define the calendar rather than reopen a period.
  PeriodNotFound = 2,

  // An account named by a line does not exist, or is not active. Payroll validates mapping at APPROVAL
  // (`OD-PAY-0012`), so reaching this at posting means something changed in between -- which is precisely
  // why it is still checked here and not assumed.
  AccountUnavailable = 3,

  // Debits did not equal credits (`BR-GL-0001`). If Payroll ever sees this, Payroll has a calculation
  // defect: it is included so that defect surfaces as a refusal rather than as a malformed ledger.
  Unbalanced = 4,

  // The journal named for reversal does not exist, or has already been reversed.
  ReversalTargetUnavailable = 5,

  // ---- 249. THE PERIOD'S STATE IS BEING CHANGED RIGHT NOW. RETRY.
  //
  // Named for the SITUATION and not for the mechanism. A poster takes a shared fence before reading the
  // period and a period-state change takes it exclusively; this is what a caller is told when the fence
  // could not be taken in time. ⚠ THE NAME MUST NOT MENTION THE FENCE: a consumer needs its own next
  // action, not `sp_getapplock`, and the day the mechanism changes a mechanism-shaped name becomes a lie
  // that other modules switch on. An internal name is a rename; a contract member is a migration.
  //
  // ⚠⚠ AND IT IS THE ONE MEMBER WHOSE DISTINCTION IS ACTIONABLE. For the others a generic refusal is
  // merely unhelpful; for this one it is WRONG, because the caller's correct response is to do the same
  // thing again, and "the ledger refused the posting" tells them to stop.
  PeriodStateChanging = 6
}

// ---- THE OUTCOME.
//
// `JournalEntryId` is populated only on `Posted`. `PeriodName` is populated only for `PeriodClosed`, because
// that is the one refusal whose message has to name something to be useful to a human.
public sealed record JournalPostingOutcome(
  JournalPostingStatus Status,
  Guid? JournalEntryId,
  string? PeriodName,
  string? Detail)
{
  public bool IsPosted => Status == JournalPostingStatus.Posted;

  public static JournalPostingOutcome Success(Guid journalEntryId) =>
    new(JournalPostingStatus.Posted, journalEntryId, null, null);

  public static JournalPostingOutcome Closed(string periodName) =>
    new(JournalPostingStatus.PeriodClosed, null, periodName, null);

  public static JournalPostingOutcome Refused(JournalPostingStatus status, string? detail = null) =>
    new(status, null, null, detail);
}

// ---- INSPECTING THE WINDOW BEFORE COMMITTING TO IT.
//
// `OD-PAY-0014` requires Payroll to refuse a run **at approval**, naming the closed period. Posting happens
// later, at the Approved -> Posted transition, so the posting call cannot be what discovers it: by then the
// run would already be Approved and unpostable — a state with no legitimate exit, which is exactly the
// outcome the ruling chose approval-time refusal to avoid.
//
// So the consumer needs to ASK, without writing anything. This is that question, and it exists because
// Payroll needs it — not because a ledger contract would naturally offer introspection.
//
// It is explicitly NOT a reservation and NOT a promise: a period open when this returns may be closed by the
// time the posting runs. That race is why `PostAsync` still answers `PeriodClosed` and why Payroll still
// refuses the transition on it. This narrows a window that cannot be closed by asking politely.
public enum PostingWindowStatus
{
  Open = 0,
  PeriodClosed = 1,
  PeriodNotFound = 2,

  // ---- TWO FISCAL YEARS COVER THIS DATE (T-188).
  //
  // ⚠ **THIS EXISTS BECAUSE `PeriodNotFound` PRESCRIBES A HARMFUL REMEDY HERE, NOT MERELY AN UNHELPFUL
  // ONE.** `JournalPostingStatus.PeriodNotFound` states the remedy in its own comment: *"define the
  // calendar rather than reopen a period."* **An operator whose real problem is that TWO years already
  // cover the date, following that instruction, defines a THIRD.**
  //
  // This contract's design rule is that anything Payroll could not act on differently is not worth
  // distinguishing — and by that rule alone this would not qualify, because Payroll refuses the run
  // either way. **But the rule was already broken here once, deliberately:** `PeriodNotFound` is distinct
  // from `PeriodClosed` for OPERATOR REMEDY, not for caller action. Operator remedy is therefore already
  // a ratified reason to distinguish in this contract, and it is the reason that applies.
  //
  // ---- ADDING A VALUE IS SAFE HERE, AND THAT WAS MEASURED AT EVERY CONSUMER RATHER THAN ASSUMED.
  //
  // `PayrollRunCommandHandlers` reads this in two places. One tests `PeriodNotFound || FiscalPeriodId is
  // null`, and every field but `Status` is null on a refusal by this record's own rule. The other tests
  // `PeriodClosed` then `!IsOpen`, and `IsOpen` is `Status == Open`. **Both refuse an unknown status**,
  // so a widening degrades safely rather than falling through as open.
  //
  // ⚠ **PAYROLL STILL REPORTS THE MISLEADING REMEDY.** Both sites answer
  // `PayrollErrors.FiscalPeriodNotFound`, so the GL side is now honest and the payroll-facing message is
  // not. Distinguishing it there is the same argument one layer out and is NOT done here.
  CalendarAmbiguous = 3
}

// Carries the period's IDENTITY AND BOUNDS as well as its status, because Payroll needs both and asking
// twice would be two answers about one calendar with a race between them.
//
// `OD-PAY-0002` ruled a payroll period maps to exactly ONE fiscal period, and `PayrollPeriod.CreateAlignedTo`
// is built so alignment is guaranteed by construction rather than validated afterwards — it takes the fiscal
// period's identity and bounds and is not permitted to disagree with them. That constructor needs this data,
// and this is the only sanctioned way for Payroll to obtain it.
//
// Everything here is null on a refusal except `Status`. A caller that reads `FiscalPeriodId` without
// checking `IsOpen` gets `null` rather than a plausible-looking wrong answer.
public sealed record PostingWindow(
  PostingWindowStatus Status,
  string? PeriodName,
  Guid? FiscalPeriodId = null,
  DateTimeOffset? StartUtc = null,
  DateTimeOffset? EndUtc = null)
{
  public bool IsOpen => Status == PostingWindowStatus.Open;
}
