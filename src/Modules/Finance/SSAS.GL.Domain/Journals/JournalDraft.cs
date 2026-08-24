using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Journals;

// A LINE ON A DRAFT. Mutable with its draft, and replaced wholesale rather than edited in place.
public sealed class JournalDraftLine : Entity<Guid>, ITenantOwnedEntity
{
  internal JournalDraftLine(
    Guid id, Guid journalDraftId, int lineNumber, Guid accountId, decimal debit, decimal credit, string? description)
    : base(id)
  {
    JournalDraftId = journalDraftId;
    LineNumber = lineNumber;
    AccountId = accountId;
    Debit = debit;
    Credit = credit;
    Description = description;
  }

  // EF materialization only.
  private JournalDraftLine(Guid id)
    : base(id)
  {
  }

  // ---- ITenantOwnedEntity IS NOT OPTIONAL ON AN OWNED CHILD, AND THE REASON IS THE CUTOVER.
  //
  // `TenantCutoverCopyPlan.Build` derives the E3 manifest by reflecting over `ITenantOwnedEntity`. A table
  // whose type does not implement it is absent from the manifest, and therefore **absent from Shared to
  // Dedicated cutover** — which fails SILENTLY, taking the rows with it. HR's owned children carry the
  // marker for exactly this reason (`DepartmentManager` is the precedent).
  //
  // The parent's ownership is not inherited by the child at the model level. Being owned is a domain fact;
  // being copied is a reflection fact, and only the interface expresses the second.
  public Guid TenantId { get; set; }

  public Guid JournalDraftId { get; private set; }

  public int LineNumber { get; private set; }

  public Guid AccountId { get; private set; }

  // ---- TWO COLUMNS, NOT ONE SIGNED AMOUNT. Decided here, once, as `data-model.md` asked.
  //
  // The package flagged the choice and deliberately did not make it, because it is invisible outside the
  // module and belongs to whoever writes the schema. Two columns is the accounting-native shape: it makes
  // `BR-GL-0001` a direct comparison of two sums rather than a test that a signed total is zero, and it makes
  // a debit line and a credit line distinguishable in the data without knowing the sign convention. Both are
  // `decimal(19,4)` per `DEC-GL-0001`.
  public decimal Debit { get; private set; }

  public decimal Credit { get; private set; }

  public string? Description { get; private set; }
}

// THE EDITABLE JOURNAL (OD-GL-0007, option 3).
//
// ================================================================================================
// THIS IS THE MUTABLE HALF OF THE TWO-AGGREGATE MODEL, AND IT EXISTS SO THE OTHER HALF CAN BE
// APPEND-ONLY FROM CREATION.
// ================================================================================================
//
// `IAppendOnlyEntity` is a property of a TYPE, not of a state. A single aggregate that was drafted, edited
// and then posted would have to be `Modified` to reach the posted state, so it could not carry the marker at
// all — and `BR-GL-0002` would degrade from a guarantee the write boundary enforces into an aggregate-level
// check a future path could bypass. `OD-GL-0007` weighed exactly that and chose two types.
//
// So this one is freely mutable and freely deletable, carries `RowVersion`, and never becomes a
// `JournalEntry`. Posting READS it and CREATES a separate append-only aggregate; the draft is then discarded
// by the caller. Nothing is promoted in place.
//
// A draft is NOT required to balance. That is the point of a draft: work in progress that does not yet
// satisfy `BR-GL-0001` is exactly what a user needs somewhere to put. The balance rule is checked at POST
// time (`DEC-GL-0008`), where it is a precondition of a journal existing at all.
public sealed class JournalDraft : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private readonly List<JournalDraftLine> lines = [];

  private JournalDraft(Guid id, DateTimeOffset entryDateUtc, string description, string? reference)
    : base(id)
  {
    EntryDateUtc = entryDateUtc;
    Description = description;
    Reference = reference;
  }

  // EF materialization only.
  private JournalDraft(Guid id)
    : base(id)
  {
    Description = null!;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  // The ACCOUNTING date, which is not `CreatedUtc`. It decides which fiscal period the posting lands in and
  // therefore whether `BR-GL-0003` refuses it. They are different facts and both are stored.
  public DateTimeOffset EntryDateUtc { get; private set; }

  public string Description { get; private set; }

  public string? Reference { get; private set; }

  public IReadOnlyCollection<JournalDraftLine> Lines => lines.AsReadOnly();

  public DateTimeOffset CreatedUtc { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[]? RowVersion { get; set; }

  public const int MaximumDescriptionLength = 512;
  public const int MaximumReferenceLength = 128;

  public static Result<JournalDraft> Create(DateTimeOffset entryDateUtc, string? description, string? reference)
  {
    var validated = ValidateText(description, reference);
    if (validated.IsFailure)
    {
      return Result.Failure<JournalDraft>(validated.Error);
    }

    return Result.Success(new JournalDraft(
      Guid.NewGuid(), entryDateUtc.ToUniversalTime(), description!.Trim(), reference?.Trim()));
  }

  public Result Update(DateTimeOffset entryDateUtc, string? description, string? reference)
  {
    var validated = ValidateText(description, reference);
    if (validated.IsFailure)
    {
      return validated;
    }

    EntryDateUtc = entryDateUtc.ToUniversalTime();
    Description = description!.Trim();
    Reference = reference?.Trim();
    return Result.Success();
  }

  // ---- LINES ARE REPLACED WHOLESALE, NOT PATCHED.
  //
  // A draft's line set is small and is always sent complete by the client that is editing it. Offering
  // add/remove/reorder operations would create ordering questions (what is line 3 after line 2 is removed?)
  // that no requirement asks for, and would let a caller construct a set the aggregate never validated as a
  // whole. Replacement means `LineNumber` is always a dense 1..n sequence assigned here.
  public Result ReplaceLines(IReadOnlyList<(Guid AccountId, decimal Debit, decimal Credit, string? Description)> replacements)
  {
    ArgumentNullException.ThrowIfNull(replacements);

    foreach (var replacement in replacements)
    {
      if (replacement.Debit < 0m || replacement.Credit < 0m)
      {
        return Result.Failure(JournalErrors.NegativeAmount);
      }

      var isDebit = replacement.Debit > 0m;
      var isCredit = replacement.Credit > 0m;
      if (isDebit == isCredit)
      {
        return Result.Failure(JournalErrors.LineNotSingleSided);
      }
    }

    lines.Clear();
    var lineNumber = 1;
    foreach (var replacement in replacements)
    {
      lines.Add(new JournalDraftLine(
        Guid.NewGuid(), Id, lineNumber, replacement.AccountId, replacement.Debit, replacement.Credit,
        replacement.Description?.Trim()));
      lineNumber++;
    }

    return Result.Success();
  }

  public decimal TotalDebits => lines.Sum(line => line.Debit);

  public decimal TotalCredits => lines.Sum(line => line.Credit);

  // ---- THE POSTING PRECONDITIONS THE DRAFT CAN JUDGE ALONE (BR-GL-0001, DEC-GL-0008).
  //
  // Enforced in the aggregate at post time rather than by a database constraint: a CHECK constraint cannot
  // see a set of sibling rows, and a trigger would put a business rule where the domain layer cannot test it.
  //
  // What this method deliberately does NOT check is the account state and the period state — those are live
  // facts about other aggregates, and asking a draft to know them would either stale-cache them or drag a
  // repository into the domain.
  public Result EnsurePostable()
  {
    if (lines.Count < 2)
    {
      return Result.Failure(JournalErrors.InsufficientLines);
    }

    return TotalDebits == TotalCredits
      ? Result.Success()
      : Result.Failure(JournalErrors.Unbalanced);
  }

  private static Result ValidateText(string? description, string? reference)
  {
    var trimmedDescription = description?.Trim();
    if (string.IsNullOrEmpty(trimmedDescription) || trimmedDescription.Length > MaximumDescriptionLength)
    {
      return Result.Failure(JournalErrors.InvalidDescription);
    }

    var trimmedReference = reference?.Trim();
    if (trimmedReference is not null && trimmedReference.Length > MaximumReferenceLength)
    {
      return Result.Failure(JournalErrors.InvalidReference);
    }

    return Result.Success();
  }
}
