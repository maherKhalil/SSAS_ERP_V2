using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.ImportExport;

// ONE DURABLE RECORD THAT EMPLOYEE DATA LEFT THE SYSTEM (FP-009, DEC-DOC-0006, SEC-DOC-0404).
//
// Same shape as `EmployeeImportRun`, higher stakes: for an export, the run record is the ONLY control that
// survives the data leaving. Everything else — the permission, the scope, the column set — acted before the
// bytes went out and cannot be re-applied afterwards.
//
// One state. An export that FAILS writes no record, because no data left the system. That is the opposite of
// the import rule, and the asymmetry is deliberate: an import run records an attempt to WRITE, while an
// export run records bytes HAVING LEFT. A failed export has nothing to disclose.
//
// ================================================================================================
// IT IS DELIBERATELY **NOT** `ICompanyOwnedEntity`, AND `CompanyId` BELOW IS DATA, NOT OWNERSHIP.
// ================================================================================================
//
// The ruling, carried verbatim because the reason is the design:
//
//   an export is a read; its audit record must never be refusable by WRITE authorization or read-only
//   callers cannot export (or worse, the audit silently gates the read).
//
// The mechanism is concrete rather than theoretical. `TenantDbContext` treats a tracked `ICompanyOwnedEntity`
// as a company-scoped WRITE: it demands a trusted company context and calls
// `AuthorizeCurrentCompanyAsync`, throwing `TenantWriteAuthorizationException` when that fails. Marking this
// record company-owned would therefore route every export through a WRITE authorization — so a caller with
// employee READ authority and no write authority could not export, and the audit record intended to observe
// the read would have become a gate on it.
//
// `CompanyId` is still stamped, because "which company's employees left" is exactly what an investigator
// asks. It is an ATTRIBUTE the application sets from the resolved scope, not an ownership discriminator the
// write boundary enforces — the same distinction `EmployeeBranchAssignment` draws when it refuses to name
// either of its branch columns `BranchId`.
//
// ---- APPEND-ONLY, ON THE `EmployeeBranchAssignment` TERMS, exactly as its sibling.
//
// No mutators, no RowVersion, structural refusal by `TenantDbContext.PreventAppendOnlyMutation`. Not modelled
// on `TenantDatabaseBackupRun`: that type carries a lifecycle, and this one is written once when there is
// nothing left to observe.
//
// ---- THE SCOPE SNAPSHOT IS WHY THIS RECORD ANSWERS THE QUESTION THAT GETS ASKED.
//
// "Who exported employee data?" is answerable from the actor alone. "Could that person have exported THIS
// employee?" is not — unless the scope in force at the time is recorded. Scope changes over time, which is
// why `AC-EMP-0026` and `AC-EMP-0024` exist, so reconstructing it later from current authorization is
// unsound.
public sealed class EmployeeExportRun : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;

  public const int ColumnSetMaximumLength = 1024;

  // The one character that may not appear inside a column name or an identifier, because it is the one that
  // separates them. Refused at construction rather than escaped: the values written here are drawn from a
  // fixed column contract and from GUIDs, so an occurrence would mean the caller is writing something else.
  private const char Separator = ',';

  private EmployeeExportRun(
    Guid id,
    Guid tenantId,
    Guid companyId,
    int rowCount,
    string columnSet,
    string scopeCompanyIds,
    string scopeBranchIds,
    DateTimeOffset executedUtc,
    string executedBy) : base(id)
  {
    TenantId = tenantId;
    CompanyId = companyId;
    RowCount = rowCount;
    ColumnSet = columnSet;
    ScopeCompanyIds = scopeCompanyIds;
    ScopeBranchIds = scopeBranchIds;
    ExecutedUtc = executedUtc.ToUniversalTime();
    ExecutedBy = executedBy;
  }

  private EmployeeExportRun()
    : base(Guid.Empty)
  {
    ColumnSet = string.Empty;
    ScopeCompanyIds = string.Empty;
    ScopeBranchIds = string.Empty;
    ExecutedBy = string.Empty;
  }

  public Guid TenantId { get; set; }

  // DATA, NOT OWNERSHIP. See the type comment: there is no `set` accessor and no `ICompanyOwnedEntity`
  // implementation, precisely so no future convention or interface can silently reclassify this record as a
  // company-scoped write.
  public Guid CompanyId { get; private set; }

  public int RowCount { get; private set; }

  // WHAT ACTUALLY LEFT THE SYSTEM, in the order it left (`SEC-DOC-0404`). The ordered column names, not the
  // column names the contract says an export has — those two are the same only while nothing has drifted,
  // and this record exists for the case where something did.
  public string ColumnSet { get; private set; }

  // THE MATERIALIZED SCOPE AT EXECUTION TIME, as sorted identifier lists.
  //
  // Denormalized into two text columns rather than child tables on purpose: the value is an immutable
  // historical snapshot, never joined and never filtered on — it exists to be read by a human investigating
  // an incident. Child tables would have added two entities to the E3 manifest and two foreign-key edges to
  // the copy order for data nothing queries.
  public string ScopeCompanyIds { get; private set; }

  public string ScopeBranchIds { get; private set; }

  public DateTimeOffset ExecutedUtc { get; private set; }

  public string ExecutedBy { get; private set; }

  // Owned by the IAuditableEntity persistence infrastructure; never stamped by the Domain. No Modified pair.
  public DateTimeOffset CreatedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  // ONE FACTORY, BECAUSE THERE IS ONE STATE. A `Failed` counterpart would be a record that data left when it
  // did not, which is the single thing this table must never say.
  public static Result<EmployeeExportRun> Completed(
    Guid tenantId,
    Guid companyId,
    int rowCount,
    IReadOnlyCollection<string> columnSet,
    IReadOnlyCollection<Guid> scopeCompanyIds,
    IReadOnlyCollection<Guid> scopeBranchIds,
    DateTimeOffset executedUtc,
    string actor)
  {
    if (!IsValidActor(actor))
    {
      return Result.Failure<EmployeeExportRun>(ImportExportErrors.InvalidActor);
    }

    if (rowCount < 0)
    {
      return Result.Failure<EmployeeExportRun>(ImportExportErrors.InvalidCounts);
    }

    // AN EMPTY COLUMN SET WOULD RECORD THAT NOTHING LEFT, which is not an export. The order is the caller's
    // and is preserved exactly — sorting it here would destroy the one fact this column carries.
    if (columnSet is null || columnSet.Count == 0 ||
      columnSet.Any(column => string.IsNullOrWhiteSpace(column) || column.Contains(Separator)))
    {
      return Result.Failure<EmployeeExportRun>(ImportExportErrors.InvalidColumnSet);
    }

    var joinedColumns = string.Join(Separator, columnSet);
    if (joinedColumns.Length > ColumnSetMaximumLength)
    {
      return Result.Failure<EmployeeExportRun>(ImportExportErrors.InvalidColumnSet);
    }

    return Result.Success(new EmployeeExportRun(
      Guid.NewGuid(),
      tenantId,
      companyId,
      rowCount,
      joinedColumns,
      Sorted(scopeCompanyIds),
      Sorted(scopeBranchIds),
      executedUtc,
      actor.Trim()));
  }

  // SORTED, so two records describing the same scope are textually identical and an investigator comparing
  // them is comparing scopes rather than enumeration orders. Ordinal on the canonical GUID text, which is the
  // only ordering that does not depend on a culture.
  private static string Sorted(IReadOnlyCollection<Guid>? identifiers) =>
    identifiers is null || identifiers.Count == 0
      ? string.Empty
      : string.Join(
        Separator,
        identifiers.Select(identifier => identifier.ToString()).OrderBy(text => text, StringComparer.Ordinal));

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

  // The Modified pair is explicitly unsupported: this record is never modified, so the columns do not exist
  // and the setters are no-ops rather than fields nothing reads.
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
