using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Journals;

// THE JOURNAL'S NAMED REFUSALS (REQ-GL-0001..0004, BR-GL-0001, BR-GL-0002, BR-GL-0005).
public static class JournalErrors
{
  public static readonly Error Unbalanced = new(
    "Gl.JournalUnbalanced",
    "A journal entry must balance: the total of its debits must equal the total of its credits.");

  public static readonly Error InsufficientLines = new(
    "Gl.JournalInsufficientLines",
    "A journal entry must have at least two lines.");

  // ---- A LINE IS A DEBIT OR A CREDIT, NEVER BOTH AND NEVER NEITHER.
  //
  // With separate debit and credit columns this has to be said explicitly, because the type system permits a
  // row carrying both. A line with both is ambiguous and a line with neither is noise that still consumes a
  // line number; refusing both keeps the balance sum meaningful.
  public static readonly Error LineNotSingleSided = new(
    "Gl.JournalLineNotSingleSided",
    "Each journal line must carry either a debit or a credit amount, and not both.");

  public static readonly Error NegativeAmount = new(
    "Gl.JournalLineAmountNegative",
    "A journal line amount cannot be negative. Use the opposite side rather than a negative amount.");

  public static readonly Error InvalidDescription = new(
    "Gl.JournalDescriptionInvalid",
    "A journal description is required and must be at most 512 characters.");

  public static readonly Error InvalidReference = new(
    "Gl.JournalReferenceInvalid",
    "A journal reference must be at most 128 characters.");

  public static readonly Error DraftNotFound = new(
    "Gl.JournalDraftNotFound",
    "The journal draft does not exist.");

  public static readonly Error NotFound = new(
    "Gl.JournalNotFound",
    "The journal entry does not exist.");

  public static readonly Error NumberConflict = new(
    "Gl.JournalNumberConflict",
    "A journal with this number already exists in this fiscal year.");

  // ---- REVERSING A REVERSAL IS PERMITTED; REVERSING THE SAME JOURNAL TWICE IS NOT.
  //
  // The first is a legitimate correction of a correction. The second would double the reversal and leave the
  // books wrong by the original amount, and it is the mistake a user makes by clicking twice — so it is
  // refused by the aggregate rather than left to a unique index nobody reads.
  public static readonly Error AlreadyReversed = new(
    "Gl.JournalAlreadyReversed",
    "This journal has already been reversed.");

  // ---- ⚠ DECLARED, MAPPED TO 409, AND RETURNED BY NOTHING — DELIBERATELY. DO NOT DELETE IT (T-166).
  //
  // This looks like a dead arm and a set-difference over declared-versus-returned codes will report it as
  // one. **It is not: `BR-GL-0002` is enforced STRUCTURALLY, and this is the only trace of that rule in
  // the source.**
  //
  // ```
  // no PUT / PATCH / DELETE on /journals/{journalEntryId}    a caller cannot ask to mutate a posted journal
  // drafts.Remove(draft) in the same transaction as posting  no posted draft survives to be mutated, so
  //                                                          PUT /journal-drafts/{id} answers DraftNotFound
  // ```
  //
  // **`DEC-L-084`'s shape: an invariant the schema and the route surface hold, rather than a check.**
  // `api-contracts.md` documents this as promised behaviour backed by `BR-GL-0002`, so removing the code
  // would leave the document naming an error the source no longer contains.
  //
  // **The absence is asserted by `A_posted_journal_exposes_no_mutation_route`**, which is what stops
  // someone adding `PUT /journals/{id}` later and assuming this named refusal is live. It is not, and
  // nothing else would tell them.
  //
  // ⚠ **The previous note said a caller "attempts a mutation" and deserves a named refusal.** The intent
  // was right and the premise was wrong: **no caller can attempt one.** Corrected rather than removed,
  // because the intent is the part that matters.
  public static readonly Error Immutable = new(
    "Gl.JournalImmutable",
    "A posted journal entry cannot be modified or deleted.");
}
