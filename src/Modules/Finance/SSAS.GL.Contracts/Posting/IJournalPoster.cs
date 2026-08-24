namespace SSAS.GL.Contracts.Posting;

// ================================================================================================
// THE PORT. ONE INTERFACE, TWO OPERATIONS, IMPLEMENTED INSIDE GL.
// ================================================================================================
//
// `OD-PAY-0013` ruled a SYNCHRONOUS contract over a domain event, and the deciding property is stated here
// because it is the reason the shape looks like this:
//
//   **A payroll run must not be able to claim it posted when it did not.**
//
// An event would let the run report success while the journal silently failed -- the hardest possible defect
// to notice, because the payroll side looks correct and the ledger is simply missing a journal nobody is
// waiting for. That can be made safe with an outbox and a reconciliation loop; neither exists in this
// product, and building both to avoid a synchronous call would be a large cost for coupling that is
// genuinely one-directional.
//
// So: Payroll calls, GL answers, and a refusal REFUSES THE TRANSITION. `PayrollRun` reaches `Posted` only
// after this returns `Posted`.
//
// ---- WHO IMPLEMENTS IT, AND WHO MAY NOT SEE THAT.
//
// The implementation lives in GL's own infrastructure and is registered by the Host. Payroll references only
// this assembly (`ADR-012`); it never sees `SSAS.GL.Domain`, `.Application`, `.Infrastructure` or `.API`, and
// an architecture guard asserts the absence IN BOTH DIRECTIONS -- GL must not learn about Payroll either,
// or the "one-directional coupling" argument above would stop being true.
public interface IJournalPoster
{
  // Posts a balanced journal for a company, on a date from which GL resolves the fiscal period itself.
  //
  // This does NOT throw for a business refusal. A closed period and an unavailable account are ordinary
  // answers, not exceptions, because the caller's response to them is to refuse a state transition rather
  // than to fail. Infrastructure faults still throw, and that difference is the whole reason the outcome
  // type exists.
  Task<JournalPostingOutcome> PostAsync(
    JournalPostingRequest request,
    CancellationToken cancellationToken = default);

  // Reverses a posted journal. GL mirrors the original's lines itself, so a reversal cannot silently differ
  // from what it claims to reverse -- the caller supplies only the date and a description.
  //
  // Payroll needs this because a posted run is immutable: correcting one means reversing its journal and
  // running again (`OD-PAY-0011`, `DEC-PAY-0012`). The reversal is a NEW journal, never an edit of the
  // original, which is what keeps the ledger append-only.
  Task<JournalPostingOutcome> ReverseAsync(
    JournalReversalRequest request,
    CancellationToken cancellationToken = default);
}
