using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Runs;

public enum PayrollRunStatus
{
  Draft = 0,
  Calculated = 1,
  Approved = 2,
  Posted = 3
}

// ================================================================================================
// THE RUN. THREE TYPES, NOT TWO — RULED 2026-08-24 AFTER THE PACKAGE WAS OVERRIDDEN.
// ================================================================================================
//
// The analysis package proposed ONE aggregate with a status-dependent guard and called it "the one place
// where FP-012 deliberately diverges from GL's shape". **That divergence was the error**, and the amendment
// in `decisions-approved.md` records the mechanical proof. It is worth restating here, at the site, because
// this file is where someone would be tempted to re-simplify it:
//
//   `TenantDbContext.PreventAppendOnlyMutation` refuses `Modified` OR `Deleted` for any `IAppendOnlyEntity`,
//   UNCONDITIONALLY. It cannot know a run is still Draft. So:
//
//     * `IAppendOnlyEntity` from birth FORBIDS the wholesale line replacement `OD-PAY-0011` ruled for
//       recalculation — replacing a line set requires `Deleted`.
//     * Omitting it leaves protection BEHAVIOURAL ONLY, so the strongest guard in the codebase never engages
//       on the records stating what people were paid.
//
// GL's shape is not a style to differ from: **GL met this exact problem and `OD-GL-0007` solved it**, with
// `JournalDraftLine` mutable and `JournalLine : IAppendOnlyEntity`. FP-012 inherits the solution.
//
// ---- WHY *THIS* TYPE IS NOT APPEND-ONLY, AND WHY THAT IS ACCEPTABLE HERE.
//
// `PayrollRun` is **mutable for its whole life** and deliberately carries no `IAppendOnlyEntity`. It must
// record `PostedUtc` and `JournalEntryId` AFTER it is Approved, and an unconditional guard would refuse that
// write. Its immutability after `Posted` is therefore a **domain guard — behavioural**.
//
// **That is acceptable HERE and nowhere else in this module, because the run is the WRAPPER.** The
// truth-bearing records are the approved lines and the GL journal, and **both are structurally append-only**
// — `PayrollRunLine` through the interface, the journal through GL's own boundary. So a bug in this class
// cannot rewrite what anyone was paid or what was posted. It could at worst corrupt the wrapper's own
// status, which is recoverable and visible.
public sealed class PayrollRun
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private readonly List<PayrollRunDraftLine> draftLines = [];
  private readonly List<PayrollRunLine> lines = [];

  private PayrollRun(Guid id, Guid companyId, Guid payrollPeriodId)
    : base(id)
  {
    CompanyId = companyId;
    PayrollPeriodId = payrollPeriodId;
    Status = PayrollRunStatus.Draft;
  }

  // EF materialization only.
  private PayrollRun(Guid id)
    : base(id)
  {
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid PayrollPeriodId { get; private set; }

  public PayrollRunStatus Status { get; private set; }

  // ---- THE WORKING SET. Mutable, replaced wholesale on recalculation (`OD-PAY-0011`).
  public IReadOnlyCollection<PayrollRunDraftLine> DraftLines => draftLines.AsReadOnly();

  // ---- THE APPROVED RECORD. Written once, never touched again. The payslip projects over THIS
  // (`OD-PAY-0015`), which is why a payslip exists precisely when an approved record exists.
  public IReadOnlyCollection<PayrollRunLine> Lines => lines.AsReadOnly();

  public string? CalculatedBy { get; private set; }

  public DateTimeOffset? CalculatedUtc { get; private set; }

  public string? ApprovedBy { get; private set; }

  public DateTimeOffset? ApprovedUtc { get; private set; }

  public string? PostedBy { get; private set; }

  public DateTimeOffset? PostedUtc { get; private set; }

  // ---- WHEN THIS RUN'S POSTING WAS REVERSED (T-112). `PostedUtc`'s SHAPE EXACTLY.
  //
  // A lifecycle timestamp stamped once, on a type that is **mutable for its whole life and deliberately
  // carries no `IAppendOnlyEntity`** — see the note at the top of this file. `BR-PAY-0011`'s *"never updated
  // afterwards"* is about the LINES, which ARE append-only, and about not erasing what was attempted.
  // **Stamping a new fact erases nothing.**
  //
  // ---- WHY PAYROLL RECORDS THIS WHEN THE LEDGER ALREADY KNOWS.
  //
  // `ReversePayrollRunCommandHandler` used to write nothing here, reasoning that *"marking it reversed would
  // be a claim the ledger already makes better"* — and that reasoning was right **for GL's purposes**. GL
  // derives reversal from the reversing entry, and it should.
  //
  // **Payroll needs the fact for a different purpose: its own uniqueness rule.** One run per period is
  // enforced by a unique index, and **a filtered index cannot read another module's table.** Asking GL at
  // create time would move that authority out of the database into application code, which is the one thing
  // `PayrollRunConfiguration`'s comment says is load-bearing. So the fact is recorded here, for here.
  public DateTimeOffset? ReversedUtc { get; private set; }

  public bool IsReversed => ReversedUtc is not null;

  // ---- STAMPED AFTER THE LEDGER HAS ACCEPTED THE REVERSAL, NEVER BEFORE.
  //
  // The same discipline `Post` follows: the run records what the ledger DID, so a run cannot claim a
  // reversal that never posted. The handler calls this only once `ReverseAsync` reports success.
  //
  // Refuses a second reversal rather than restamping. A run reversed twice would mean two reversing entries
  // for one posting, and the second timestamp would quietly overwrite the record of when the first happened.
  public Result MarkReversed()
  {
    if (Status != PayrollRunStatus.Posted || JournalEntryId is null)
    {
      return Result.Failure(PayrollErrors.RunNotReversible);
    }

    if (ReversedUtc is not null)
    {
      return Result.Failure(PayrollErrors.RunAlreadyReversed);
    }

    ReversedUtc = DateTimeOffset.UtcNow;
    return Result.Success();
  }

  // The journal this run produced, by identifier. **No foreign key to GL's tables** (`DEC-PAY-0008` and the
  // module boundary): a database-level FK would couple the two modules' migrations and make the boundary a
  // fiction at the schema layer even while `ADR-012` held at the assembly layer.
  public Guid? JournalEntryId { get; private set; }

  public byte[]? RowVersion { get; set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public static Result<PayrollRun> Create(Guid companyId, Guid payrollPeriodId)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<PayrollRun>(PayrollErrors.RunCompanyRequired);
    }

    if (payrollPeriodId == Guid.Empty)
    {
      return Result.Failure<PayrollRun>(PayrollErrors.RunPeriodRequired);
    }

    return Result.Success(new PayrollRun(Guid.NewGuid(), companyId, payrollPeriodId));
  }

  // ---- CALCULATION. Free before approval, impossible after (REQ-PAY-0012, OD-PAY-0011).
  //
  // Replaces the ENTIRE draft line set rather than adjusting lines. Adjusting in place would make a payslip
  // read differently before and after an event nobody recorded.
  public Result SetCalculation(IReadOnlyList<PayrollRunDraftLine> calculated, string? calculatedBy)
  {
    ArgumentNullException.ThrowIfNull(calculated);

    if (Status is PayrollRunStatus.Approved or PayrollRunStatus.Posted)
    {
      return Result.Failure(PayrollErrors.RunNotRecalculable(Status));
    }

    draftLines.Clear();
    draftLines.AddRange(calculated);

    Status = PayrollRunStatus.Calculated;
    CalculatedBy = calculatedBy;
    CalculatedUtc = DateTimeOffset.UtcNow;
    return Result.Success();
  }

  // ---- APPROVAL. The sensitive act (BR-PLT-0103, OD-PAY-0009), and the promotion boundary.
  //
  // This is where the draft becomes the record. The append-only line set is constructed HERE and nowhere
  // else — `PayrollRunLine`'s constructor is `internal`, so nothing outside this assembly can fabricate an
  // approved pay record. That is `OD-GL-0007`'s boundary applied to payroll: *nothing outside the module
  // fabricates the immutable record*.
  //
  // No state is skipped: a Draft run has no lines to approve (`REQ-PAY-0010`).
  public Result Approve(string? approvedBy)
  {
    if (Status != PayrollRunStatus.Calculated)
    {
      return Result.Failure(PayrollErrors.RunNotApprovable(Status));
    }

    // A calculated run with no lines is not an approvable payroll — it is an empty company or a defect, and
    // approving it would post an empty journal, which `BR-GL-0001` would then refuse anyway.
    if (draftLines.Count == 0)
    {
      return Result.Failure(PayrollErrors.RunHasNoLines);
    }

    lines.Clear();
    foreach (var draft in draftLines.OrderBy(line => line.EmployeeId).ThenBy(line => line.Sequence))
    {
      lines.Add(new PayrollRunLine(
        Guid.NewGuid(), Id, draft.EmployeeId, draft.PayElementId, draft.Kind,
        draft.Amount, draft.Sequence, draft.GlAccountId));
    }

    Status = PayrollRunStatus.Approved;
    ApprovedBy = approvedBy;
    ApprovedUtc = DateTimeOffset.UtcNow;
    return Result.Success();
  }

  // ---- POSTING. Only after approval, and only once (REQ-PAY-0010, REQ-PAY-0015).
  //
  // Called after `IJournalPoster` has answered `Posted`. **A posting failure refuses the transition**
  // (`OD-PAY-0013`): the handler does not call this unless the ledger actually accepted the journal, which is
  // what makes "a run cannot claim it posted when it did not" true rather than aspirational.
  public Result MarkPosted(Guid journalEntryId, string? postedBy)
  {
    if (Status != PayrollRunStatus.Approved)
    {
      return Result.Failure(PayrollErrors.RunNotPostable(Status));
    }

    if (journalEntryId == Guid.Empty)
    {
      return Result.Failure(PayrollErrors.RunJournalRequired);
    }

    Status = PayrollRunStatus.Posted;
    JournalEntryId = journalEntryId;
    PostedBy = postedBy;
    PostedUtc = DateTimeOffset.UtcNow;
    return Result.Success();
  }

  // ---- THE TOTALS, DERIVED FROM THE STORED LINES.
  //
  // `OD-PAY-0008` ruled that a run total is the **sum of rounded lines**, so these read the stored amounts
  // rather than recomputing anything. That is what makes `AC-PAY-0026` — the payslip adds up — true by
  // construction. Recomputing here would reintroduce exactly the discrepancy the ruling removed.
  //
  // Before approval they read the draft set, after approval the append-only set, so a total always describes
  // the lines a reader can actually see.
  public decimal TotalEarnings => CurrentLines()
    .Where(line => line.Kind == Elements.PayElementKind.Earning)
    .Sum(line => line.Amount);

  public decimal TotalDeductions => CurrentLines()
    .Where(line => line.Kind == Elements.PayElementKind.Deduction)
    .Sum(line => line.Amount);

  public decimal NetPay => TotalEarnings - TotalDeductions;

  private IEnumerable<IPayrollLine> CurrentLines() =>
    Status is PayrollRunStatus.Approved or PayrollRunStatus.Posted
      ? lines
      : draftLines;
}

// A common read shape over the two line types, so the run's totals can be expressed once instead of twice.
// Deliberately NOT a shared base class: the two types differ in exactly the property that matters — one is
// `IAppendOnlyEntity` and one is not — and a base class would invite someone to "tidy" that away.
public interface IPayrollLine
{
  Guid EmployeeId { get; }

  Guid PayElementId { get; }

  Elements.PayElementKind Kind { get; }

  decimal Amount { get; }

  int Sequence { get; }
}

// ---- THE WORKING LINE. MUTABLE, AND DELIBERATELY NOT APPEND-ONLY.
//
// Follows `JournalDraftLine` exactly. The absence of `IAppendOnlyEntity` here is what makes `OD-PAY-0011`'s
// free recalculation possible at all: the set is deleted and rewritten, which the append-only guard would
// refuse. **Do not add the interface to this type.**
public sealed class PayrollRunDraftLine : Entity<Guid>, ITenantOwnedEntity, IPayrollLine
{
  public PayrollRunDraftLine(
    Guid id,
    Guid payrollRunId,
    Guid employeeId,
    Guid payElementId,
    Elements.PayElementKind kind,
    decimal amount,
    int sequence,
    Guid? glAccountId)
    : base(id)
  {
    PayrollRunId = payrollRunId;
    EmployeeId = employeeId;
    PayElementId = payElementId;
    Kind = kind;
    Amount = amount;
    Sequence = sequence;
    GlAccountId = glAccountId;
  }

  // EF materialization only.
  private PayrollRunDraftLine(Guid id)
    : base(id)
  {
  }

  public Guid TenantId { get; set; }

  public Guid PayrollRunId { get; private set; }

  public Guid EmployeeId { get; private set; }

  public Guid PayElementId { get; private set; }

  public Elements.PayElementKind Kind { get; private set; }

  public decimal Amount { get; private set; }

  // The order this line was actually evaluated in, retained so a payslip can explain itself even if the
  // element's `CalculationOrder` is edited afterwards.
  public int Sequence { get; private set; }

  // Captured at calculation time so approval can validate mapping without re-reading every element, and so
  // the posting uses the account that was in force when the numbers were produced.
  public Guid? GlAccountId { get; private set; }
}

// ---- THE APPROVED LINE. `IAppendOnlyEntity` — THE STRUCTURAL GUARD.
//
// Written ONCE, by `PayrollRun.Approve`, and never mutated. `TenantDbContext.PreventAppendOnlyMutation`
// refuses any `Modified` or `Deleted` on this type, so **no code path in the product can rewrite what
// someone was paid** — not a handler bug, not a repository, not a future feature that has forgotten why this
// matters. That is the difference between a structural guard and a promise.
//
// The constructor is `internal` on `OD-GL-0007`'s reasoning: an approved pay record may be created only by
// approving a run, exactly as a posted journal may be created only by posting a draft.
public sealed class PayrollRunLine : Entity<Guid>, ITenantOwnedEntity, IAppendOnlyEntity, IPayrollLine
{
  internal PayrollRunLine(
    Guid id,
    Guid payrollRunId,
    Guid employeeId,
    Guid payElementId,
    Elements.PayElementKind kind,
    decimal amount,
    int sequence,
    Guid? glAccountId)
    : base(id)
  {
    PayrollRunId = payrollRunId;
    EmployeeId = employeeId;
    PayElementId = payElementId;
    Kind = kind;
    Amount = amount;
    Sequence = sequence;
    GlAccountId = glAccountId;
  }

  // EF materialization only.
  private PayrollRunLine(Guid id)
    : base(id)
  {
  }

  public Guid TenantId { get; set; }

  public Guid PayrollRunId { get; private set; }

  public Guid EmployeeId { get; private set; }

  public Guid PayElementId { get; private set; }

  public Elements.PayElementKind Kind { get; private set; }

  public decimal Amount { get; private set; }

  public int Sequence { get; private set; }

  public Guid? GlAccountId { get; private set; }
}
