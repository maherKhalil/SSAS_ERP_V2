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

  // Kept for the API surface even though the write boundary is what actually enforces it: a caller who
  // attempts a mutation deserves a named refusal rather than an infrastructure exception surfacing raw.
  public static readonly Error Immutable = new(
    "Gl.JournalImmutable",
    "A posted journal entry cannot be modified or deleted.");
}
