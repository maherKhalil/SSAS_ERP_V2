using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Application.ImportExport;

// THE RUN-HISTORY READ MODELS (FR-DOC-0103, FR-DOC-0202, DEC-DOC-0016).
//
// ================================================================================================
// WHAT IS ABSENT FROM `EmployeeExportRunListItem` IS THE DECISION (DEC-DOC-0016).
// ================================================================================================
//
// `EmployeeExportRun` carries `ScopeCompanyIds` and `ScopeBranchIds` — the materialized scope at execution
// time, as sorted identifier lists. **Neither is on this type**, and there is therefore no projection that
// could leak them and no flag that could re-add them.
//
// The reason is the disclosure shape this module collapses everywhere else. Both run-history routes are
// gated on `HR.Employees.View` and scoped to the caller's companies, so caller A reads caller B's export run
// inside the same company. Shipping the snapshot would hand A a sorted list of company and branch
// identifiers that B's authority admitted and A's may not — the same information `EmployeeApiErrorMapper`
// refuses to disclose when it gives unauthorized, inactive, wrong-tenant and nonexistent companies one
// identical code, because *"the difference is exactly the information they are not allowed to have"*. An
// audit surface that leaked scope identifiers under a general read permission would undo that discipline in
// the one place records are designed to be read broadly.
//
// `FR-DOC-0202`'s stated point survives whole: it asks for *"the record of what column set left"*, and
// `ColumnSet` is here. The snapshot's value is investigation-grade rather than list-grade — it stays durable
// in the table for whoever investigates with database access, or later, a privileged surface. **Exposing it
// on a privileged audit route later is ADDITIVE; exposing it now and retracting later is not**, and that
// asymmetry is the whole argument for omitting it while the question is open.
//
// `FileName` and `ImportKey` DO ship. Both are values the caller supplied, echoed back to a company-scoped
// listing they already have authority over: intra-company visibility of who imported which file under which
// key is the audit trail working as intended rather than a leak.

// One import run, as the history route may see it.
public sealed record EmployeeImportRunListItem(
  Guid ImportRunId,
  string ImportKey,
  string FileName,
  int ByteCount,
  int RowCount,
  int AcceptedCount,
  int RejectedCount,
  EmployeeImportOutcome Outcome,
  DateTimeOffset ExecutedUtc,
  string ExecutedBy);

// One export run. `ColumnSet` is a LIST rather than the stored comma-joined string: the storage form is a
// denormalization decision (`data-model.md`), not a contract, and re-splitting here means the wire shape
// says "these columns, in this order" instead of making every caller parse it the same way.
public sealed record EmployeeExportRunListItem(
  Guid ExportRunId,
  int RowCount,
  IReadOnlyList<string> ColumnSet,
  DateTimeOffset ExecutedUtc,
  string ExecutedBy);

// PAGING, ON THE MODULE'S ESTABLISHED TERMS.
//
// The same three numbers every other list in HR uses — 1, 50, 200 — restated here rather than borrowed from
// `EmployeeSearchCriteria`, because that is the convention the module already follows: `DepartmentSearchCriteria`
// carries its own, and a shared constant would make one surface's paging contract a dependency of another's.
//
// The MAXIMUM IS A HARD CEILING, and an out-of-range request is REFUSED rather than clamped — silently
// reducing a page size of 5,000 to 200 would return a page the caller did not ask for and let them believe
// they had seen the rest.
public sealed record EmployeeRunHistoryCriteria(
  int PageNumber = EmployeeRunHistoryCriteria.DefaultPageNumber,
  int PageSize = EmployeeRunHistoryCriteria.DefaultPageSize)
{
  public const int DefaultPageNumber = 1;

  public const int DefaultPageSize = 50;

  public const int MaxPageSize = 200;
}
