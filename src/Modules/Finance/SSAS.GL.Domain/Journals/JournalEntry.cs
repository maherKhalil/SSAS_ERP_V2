using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Journals;

// A POSTED LINE. APPEND-ONLY WITH ITS ENTRY (BR-GL-0002, DEC-GL-0002).
public sealed class JournalLine : Entity<Guid>, ITenantOwnedEntity, IAppendOnlyEntity
{
  internal JournalLine(
    Guid id, Guid journalEntryId, int lineNumber, Guid accountId, decimal debit, decimal credit, string? description)
    : base(id)
  {
    JournalEntryId = journalEntryId;
    LineNumber = lineNumber;
    AccountId = accountId;
    Debit = debit;
    Credit = credit;
    Description = description;
  }

  // EF materialization only.
  private JournalLine(Guid id)
    : base(id)
  {
  }

  public Guid TenantId { get; set; }

  public Guid JournalEntryId { get; private set; }

  public int LineNumber { get; private set; }

  public Guid AccountId { get; private set; }

  public decimal Debit { get; private set; }

  public decimal Credit { get; private set; }

  public string? Description { get; private set; }

  // No `RowVersion` (`DEC-GL-0007`). There is no concurrent update to detect, because the write boundary
  // refuses updates to this type entirely; a version column here would advertise a mutation that cannot
  // happen and would invite someone to write the update path it implies.
}

// THE POSTED JOURNAL (REQ-GL-0001..0004, BR-GL-0001..0005, OD-GL-0006, OD-GL-0007).
//
// ================================================================================================
// APPEND-ONLY FROM CREATION, AND CREATABLE ONLY BY POSTING A DRAFT.
// ================================================================================================
//
// `IAppendOnlyEntity` makes `TenantDbContext.PreventAppendOnlyMutation` refuse any `Modified` or `Deleted`
// entry for this type **by whatever path tracked it** — repository, direct context attach, or a path nobody
// has written yet. That is the whole reason `OD-GL-0007` chose two aggregates: the interface's own comment
// records that "there is no repository method for it" protects only the callers who go through the
// repository, and `BR-GL-0002` deserves better than that.
//
// GL is the pattern's largest client. HR's append-only rows are assignment history and run records —
// incidental to their aggregates. Here the append-only record IS the aggregate, and it is the module's
// highest-volume table.
//
// ---- "REVERSED" IS NOT A STATE ON THE ORIGINAL.
//
// It is a fact derivable from the existence of a reversing journal that points at it. Storing a
// `Status = Reversed` flag on the original would require MODIFYING AN APPEND-ONLY ROW, which the write
// boundary refuses. Anyone who finds themselves wanting that column has found the guarantee working, not a
// limitation to route around — and the read model answers "was this reversed?" with a join, which costs a
// query and preserves an invariant.
public sealed class JournalEntry : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IAppendOnlyEntity
{
  private readonly List<JournalLine> lines = [];

  private JournalEntry(
    Guid id,
    Guid companyId,
    Guid fiscalYearId,
    Guid fiscalPeriodId,
    string journalNumber,
    DateTimeOffset entryDateUtc,
    string description,
    string? reference,
    Guid? reversesJournalEntryId)
    : base(id)
  {
    CompanyId = companyId;
    FiscalYearId = fiscalYearId;
    FiscalPeriodId = fiscalPeriodId;
    JournalNumber = journalNumber;
    EntryDateUtc = entryDateUtc;
    Description = description;
    Reference = reference;
    ReversesJournalEntryId = reversesJournalEntryId;
  }

  // EF materialization only.
  private JournalEntry(Guid id)
    : base(id)
  {
    JournalNumber = null!;
    Description = null!;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid FiscalYearId { get; private set; }

  // Resolved from `EntryDateUtc` at post time, never supplied by the caller (`AC-GL-0002`).
  public Guid FiscalPeriodId { get; private set; }

  // Unique within (CompanyId, FiscalYearId) — `BR-GL-0005` as scoped by `OD-GL-0004`.
  //
  // UNIQUE, NOT GAPLESS. `BR-GL-0005` asks for unique and this delivers unique; gaplessness is a materially
  // harder obligation that nobody has agreed, and `AC-GL-0013` deliberately does not assert it.
  public string JournalNumber { get; private set; }

  public DateTimeOffset EntryDateUtc { get; private set; }

  public string Description { get; private set; }

  public string? Reference { get; private set; }

  // `OD-GL-0006`: the correction is discoverable from either side. Null on an original, set on a reversal.
  public Guid? ReversesJournalEntryId { get; private set; }

  public IReadOnlyCollection<JournalLine> Lines => lines.AsReadOnly();

  public DateTimeOffset CreatedUtc { get; set; }

  // `ModifiedUtc` and `ModifiedBy` exist because `IAuditableEntity` declares them, and on this type they
  // stay at their creation values forever: the write boundary refuses the modification that would advance
  // them. That is a property worth asserting rather than assuming, and `TS-GL-0009` does.
  public DateTimeOffset ModifiedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public string? ModifiedBy { get; set; }

  // ---- POSTING. The ONLY way a JournalEntry comes into existence.
  //
  // `internal` so nothing outside this assembly can fabricate a posted journal that never passed through a
  // draft and its checks. The same reasoning `EmployeePositionAssignment`'s history factories used under
  // `DEC-POS-0008`, and the reason this assembly grants `InternalsVisibleTo` to its test project rather than
  // widening the surface to make it reachable.
  //
  // The caller has already established, in the writing transaction, that: the draft balances and has enough
  // lines (`EnsurePostable`), every named account is active (`EnsureCanReceiveTransactions`), and the period
  // covering the entry date is open (`ResolveOpenPeriodFor`). Those are live facts about other aggregates,
  // which is why they are the handler's to check and not this method's.
  internal static JournalEntry Post(
    JournalDraft draft,
    Guid fiscalYearId,
    Guid fiscalPeriodId,
    string journalNumber,
    Guid? reversesJournalEntryId = null)
  {
    ArgumentNullException.ThrowIfNull(draft);

    var entry = new JournalEntry(
      Guid.NewGuid(),
      draft.CompanyId,
      fiscalYearId,
      fiscalPeriodId,
      journalNumber,
      draft.EntryDateUtc,
      draft.Description,
      draft.Reference,
      reversesJournalEntryId);

    foreach (var line in draft.Lines.OrderBy(line => line.LineNumber))
    {
      entry.lines.Add(new JournalLine(
        Guid.NewGuid(), entry.Id, line.LineNumber, line.AccountId, line.Debit, line.Credit, line.Description));
    }

    return entry;
  }

  // ---- THE REVERSAL, BUILT FROM THE ORIGINAL RATHER THAN FROM CALLER INPUT.
  //
  // Debits become credits and credits become debits, line for line. The caller supplies nothing but the
  // number and the period, so a reversal cannot silently differ from what it claims to reverse — which is
  // the failure mode that would make `AC-GL-0006` untestable in practice.
  internal static JournalEntry Reverse(
    JournalEntry original,
    Guid fiscalPeriodId,
    string journalNumber,
    DateTimeOffset reversalDateUtc,
    string description)
  {
    ArgumentNullException.ThrowIfNull(original);

    var reversal = new JournalEntry(
      Guid.NewGuid(),
      original.CompanyId,
      original.FiscalYearId,
      fiscalPeriodId,
      journalNumber,
      reversalDateUtc.ToUniversalTime(),
      description,
      original.Reference,
      original.Id);

    foreach (var line in original.lines.OrderBy(line => line.LineNumber))
    {
      reversal.lines.Add(new JournalLine(
        Guid.NewGuid(), reversal.Id, line.LineNumber, line.AccountId, line.Credit, line.Debit, line.Description));
    }

    return reversal;
  }

  public decimal TotalDebits => lines.Sum(line => line.Debit);

  public decimal TotalCredits => lines.Sum(line => line.Credit);
}
