using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.ImportExport;

// ONE DURABLE RECORD THAT AN IMPORT HAPPENED (FP-009, DEC-DOC-0006).
//
// It is an ENTITY, not an aggregate root: a durable fact about something that already finished, with no
// lifecycle after it is written and no invariant to protect over time. "Import" itself is deliberately not an
// aggregate — an import is a use case that composes `Employee.Create` N times, and modelling it as one would
// create a SECOND place where an employee can come into existence.
//
// ================================================================================================
// THE OWNERSHIP ASYMMETRY WITH `EmployeeExportRun` IS DELIBERATE. THIS HALF IS THE COMPANY-OWNED ONE.
// ================================================================================================
//
// This record is `ICompanyOwnedEntity`: an importer is by definition a company-scope writer; the run record
// rides the same boundary as the rows it imports (including a REFUSED file, where the run record is the only
// write — the importer's authority still covers it).
//
// Its sibling is NOT company-owned, and that difference is the whole design rather than an inconsistency.
// See `EmployeeExportRun`, where the reason is stated in full.
//
// ---- APPEND-ONLY, ON THE `EmployeeBranchAssignment` TERMS.
//
// No mutators, no RowVersion, and the refusal is structural: `TenantDbContext.PreventAppendOnlyMutation`
// rejects a Modified or Deleted entry for any `IAppendOnlyEntity` whatever path tracked it. A record of what
// happened that can be edited afterwards is not a record of what happened.
//
// It is NOT modelled on `TenantDatabaseBackupRun`, which carries a lifecycle — a run that starts, is observed
// and ends. This one has no such shape: it is written once, when the outcome is already known.
//
// ---- ROWS ARE COUNTED, NEVER STORED.
//
// Rejected rows are reported in the response (`DEC-DOC-0003`) and counted here. Persisting every rejected row
// would mean storing rejected PII indefinitely with no rule saying for how long, which is a worse outcome
// than making the operator keep their own file. The submitted file is not stored either, for the same reason.
public sealed class EmployeeImportRun
  : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;

  public const int FileNameMaximumLength = 260;

  private EmployeeImportRun(
    Guid id,
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    string fileName,
    int byteCount,
    int rowCount,
    int acceptedCount,
    int rejectedCount,
    EmployeeImportOutcome outcome,
    DateTimeOffset executedUtc,
    string executedBy) : base(id)
  {
    TenantId = tenantId;
    CompanyId = companyId;
    ImportKey = importKey;
    NormalizedImportKey = importKey.NormalizedValue;
    FileName = fileName;
    ByteCount = byteCount;
    RowCount = rowCount;
    AcceptedCount = acceptedCount;
    RejectedCount = rejectedCount;
    Outcome = outcome;
    ExecutedUtc = executedUtc.ToUniversalTime();
    ExecutedBy = executedBy;
  }

  private EmployeeImportRun()
    : base(Guid.Empty)
  {
    ImportKey = null!;
    NormalizedImportKey = string.Empty;
    FileName = string.Empty;
    ExecutedBy = string.Empty;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public ImportKey ImportKey { get; private set; }

  // THE PERSISTED NORMALIZED COLUMN, not a computed projection of the value object. `DEC-POS-0030`: EF
  // translates a value-converted property in a PROJECTION but not in a PREDICATE, so uniqueness and lookup
  // run on this column — never on the display value, which no index can serve.
  public string NormalizedImportKey { get; private set; }

  // Recorded for the audit trail; never used to locate anything, and never interpreted.
  public string FileName { get; private set; }

  public int ByteCount { get; private set; }

  public int RowCount { get; private set; }

  public int AcceptedCount { get; private set; }

  public int RejectedCount { get; private set; }

  public EmployeeImportOutcome Outcome { get; private set; }

  public DateTimeOffset ExecutedUtc { get; private set; }

  public string ExecutedBy { get; private set; }

  // CreatedUtc/CreatedBy are owned by the IAuditableEntity persistence infrastructure and never stamped by
  // the Domain. There is no Modified pair: this record is never modified. `ExecutedUtc`/`ExecutedBy` are the
  // DOMAIN's own answer to who ran what and when, and they are not the same fact as the audit stamp — the
  // same separation `EmployeeBranchAssignment` draws with `TransferredBy`.
  public DateTimeOffset CreatedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  // ---- ONE FACTORY PER OUTCOME, SO AN IMPOSSIBLE RUN CANNOT BE CONSTRUCTED.
  //
  // A single `Create(outcome, accepted, rejected, …)` would let a caller write `Applied` with 998 of 1000
  // accepted — a response shape `api-contracts.md` records as NO LONGER REACHABLE under `OD-DOC-003`. Three
  // factories make the count relationships structural rather than conventional, which is what lets a test
  // assert the property instead of a reviewer remembering it.

  // THE DRY RUN. Every row passed and nothing was written; reachable only through `FR-DOC-0101`.
  public static Result<EmployeeImportRun> Validated(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    string? fileName,
    int byteCount,
    int rowCount,
    DateTimeOffset executedUtc,
    string actor) =>
    Create(
      tenantId, companyId, importKey, fileName, byteCount, rowCount,
      acceptedCount: rowCount, rejectedCount: 0,
      EmployeeImportOutcome.Validated, executedUtc, actor);

  // EMPLOYEES WERE CREATED — ALL OF THEM. `AcceptedCount` is `RowCount` by construction here, which is
  // `OD-DOC-003`'s all-or-nothing ruling expressed as a type rather than as a comment.
  public static Result<EmployeeImportRun> Applied(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    string? fileName,
    int byteCount,
    int rowCount,
    DateTimeOffset executedUtc,
    string actor) =>
    Create(
      tenantId, companyId, importKey, fileName, byteCount, rowCount,
      acceptedCount: rowCount, rejectedCount: 0,
      EmployeeImportOutcome.Applied, executedUtc, actor);

  // NOTHING WAS WRITTEN AND THE KEY IS STILL CONSUMED.
  //
  // `rejectedCount` may legitimately be ZERO: a bad header or an exceeded cap refuses the file before any row
  // is validated, and there is nothing to count. It may never exceed `rowCount`, which is the only relation
  // between the two that is always true.
  public static Result<EmployeeImportRun> Refused(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    string? fileName,
    int byteCount,
    int rowCount,
    int rejectedCount,
    DateTimeOffset executedUtc,
    string actor) =>
    Create(
      tenantId, companyId, importKey, fileName, byteCount, rowCount,
      acceptedCount: 0, rejectedCount,
      EmployeeImportOutcome.Refused, executedUtc, actor);

  private static Result<EmployeeImportRun> Create(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    string? fileName,
    int byteCount,
    int rowCount,
    int acceptedCount,
    int rejectedCount,
    EmployeeImportOutcome outcome,
    DateTimeOffset executedUtc,
    string actor)
  {
    if (importKey is null)
    {
      return Result.Failure<EmployeeImportRun>(ImportExportErrors.InvalidImportKey);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeImportRun>(ImportExportErrors.InvalidActor);
    }

    var trimmedFileName = fileName?.Trim();
    if (string.IsNullOrEmpty(trimmedFileName) || trimmedFileName.Length > FileNameMaximumLength)
    {
      return Result.Failure<EmployeeImportRun>(ImportExportErrors.InvalidFileName);
    }

    // A NEGATIVE COUNT DESCRIBES NOTHING, and a rejected count above the row count describes a file that
    // rejected rows it did not contain. Both are refused here rather than left to a check constraint alone,
    // so the pipeline learns about its own arithmetic error before the database does.
    if (byteCount < 0 || rowCount < 0 || acceptedCount < 0 || rejectedCount < 0 || rejectedCount > rowCount)
    {
      return Result.Failure<EmployeeImportRun>(ImportExportErrors.InvalidCounts);
    }

    return Result.Success(new EmployeeImportRun(
      Guid.NewGuid(), tenantId, companyId, importKey, trimmedFileName, byteCount, rowCount,
      acceptedCount, rejectedCount, outcome, executedUtc, actor.Trim()));
  }

  private static bool IsValidActor(string actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Trim().Length <= ActorMaximumLength;

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  // ---- THE MODIFIED PAIR IS EXPLICITLY UNSUPPORTED, exactly as on `EmployeeBranchAssignment`.
  //
  // IAuditableEntity requires it, but this record is never modified, so the columns do not exist and the
  // setters are no-ops rather than fields nothing reads. A getter returning the created value keeps the
  // contract honest without implying a modification that cannot happen.
  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => CreatedUtc;
    set { }
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => CreatedBy;
    set { }
  }
}
