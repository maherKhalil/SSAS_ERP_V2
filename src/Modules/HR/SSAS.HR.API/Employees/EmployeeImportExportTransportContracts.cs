using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.ImportExport;

namespace SSAS.HR.API.Employees;

// ==================================================================================================
// THE IMPORT AND EXPORT WIRE CONTRACTS (FP-009 api-contracts).
// ==================================================================================================
//
// There are no REQUEST types here, and that is the surface's shape rather than an omission: the import's
// payload is the CSV body itself (`DEC-DOC-0014`), and its one metadata field travels as an allowlisted
// query parameter. The export takes only the employee-search vocabulary. So everything below is a response.

// One thing wrong with one row (`DEC-DOC-0003`).
//
// `rowNumber` is the 1-based line number in the submitted file, HEADER INCLUDED — the number the operator's
// editor shows them. `column` is null for a problem that belongs to the row rather than to a single cell;
// a ragged row has no offending column and naming one would be a guess.
public sealed record EmployeeImportErrorResponse(
  int RowNumber,
  string? Column,
  string Code,
  string Message);

// The per-row report.
//
// `acceptedCount` is either `rowCount` or `0` and never anything between (`OD-DOC-003`) — the domain's three
// factories make that structural, so this shape cannot express a partial import even if a caller expected
// one.
public sealed record EmployeeImportReportResponse(
  Guid ImportRunId,
  string Outcome,
  int RowCount,
  int AcceptedCount,
  int RejectedCount,
  IReadOnlyCollection<EmployeeImportErrorResponse> Errors)
{
  public static EmployeeImportReportResponse From(EmployeeImportReport report) => new(
    report.ImportRunId,
    report.Outcome.ToString(),
    report.RowCount,
    report.AcceptedCount,
    report.RejectedCount,
    [.. report.Errors.Select(error => new EmployeeImportErrorResponse(
      error.RowNumber,
      error.Column,
      EmployeeImportRowErrorMapper.Map(error.Error).Code,
      error.Error.Message))]);
}

// ---- ONE IMPORT RUN, AS THE HISTORY ROUTE REPORTS IT (FR-DOC-0103, DEC-DOC-0016).
public sealed record EmployeeImportRunResponse(
  Guid ImportRunId,
  string ImportKey,
  string FileName,
  int ByteCount,
  int RowCount,
  int AcceptedCount,
  int RejectedCount,
  string Outcome,
  DateTimeOffset ExecutedUtc,
  string ExecutedBy)
{
  public static EmployeeImportRunResponse From(EmployeeImportRunListItem item) => new(
    item.ImportRunId,
    item.ImportKey,
    item.FileName,
    item.ByteCount,
    item.RowCount,
    item.AcceptedCount,
    item.RejectedCount,
    item.Outcome.ToString(),
    item.ExecutedUtc,
    item.ExecutedBy);
}

// ---- ONE EXPORT RUN (FR-DOC-0202, DEC-DOC-0016).
//
// `columnSet` ships — it is the record of WHAT LEFT, and the whole reason `FR-DOC-0202` calls this the more
// important of the two histories. The materialized SCOPE SNAPSHOT does not, and cannot: the read model this
// projects from has no property holding it, so there is nothing here to omit and nothing a later edit could
// re-expose without first changing the layer beneath.
public sealed record EmployeeExportRunResponse(
  Guid ExportRunId,
  int RowCount,
  IReadOnlyCollection<string> ColumnSet,
  DateTimeOffset ExecutedUtc,
  string ExecutedBy)
{
  public static EmployeeExportRunResponse From(EmployeeExportRunListItem item) => new(
    item.ExportRunId,
    item.RowCount,
    item.ColumnSet,
    item.ExecutedUtc,
    item.ExecutedBy);
}

// The paged envelopes, in the shape `EmployeePageResponse` established.
public sealed record EmployeeImportRunPageResponse(
  IReadOnlyCollection<EmployeeImportRunResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages)
{
  public static EmployeeImportRunPageResponse From(PagedResult<EmployeeImportRunListItem> page) => new(
    [.. page.Items.Select(EmployeeImportRunResponse.From)],
    page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages);
}

public sealed record EmployeeExportRunPageResponse(
  IReadOnlyCollection<EmployeeExportRunResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages)
{
  public static EmployeeExportRunPageResponse From(PagedResult<EmployeeExportRunListItem> page) => new(
    [.. page.Items.Select(EmployeeExportRunResponse.From)],
    page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages);
}

// ==================================================================================================
// THE ROW-ERROR CODE PROJECTION (R8) — A SEPARATE SURFACE WITH A SEPARATE CONTRACT.
// ==================================================================================================
//
// Deliberately NOT `EmployeeApiErrorMapper`. That mapper answers "what HTTP status and code does this
// failure give a ROUTE", and its answers are tuned for that: it maps every department failure to
// `request.invalid` because, as a route-level answer about the caller's own request, that is right.
//
// A row error is a different question — "what does this line of the operator's file say, in their report" —
// and `api-contracts.md` fixes different codes for it: `department.not_found` and `position.not_found`
// appear HERE and nowhere else. Reusing the route mapper would have forced one of two bad outcomes: change
// its department arm and alter the CREATE route's established answer, or accept that the report says
// `request.invalid` for a row whose department code was simply wrong, which tells the operator nothing.
//
// So: two surfaces, two contracts, and the route mapper's semantics stay untouched.
//
// ---- IT REUSES EMPLOYEE-DOMAIN CODES WHEREVER THE CONDITION IS GENUINELY THE EXISTING ONE.
//
// A row failing uniqueness fails it for exactly the reason a single create would, so it reports
// `employee.number_conflict` — the same code, from the same namespace. Inventing import-specific names for
// identical conditions would give one failure two names and let them drift.
internal static class EmployeeImportRowErrorMapper
{
  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- CONDITIONS THAT ARE GENUINELY THE EXISTING ONES. Same codes a single create answers with.
      "Employee.NumberConflict" => EmployeeApiErrorMapper.NumberConflict,
      "Employee.NationalIdConflict" => EmployeeApiErrorMapper.NationalIdConflict,

      // ---- THE TWO CLASSIFICATION CODES THAT LIVE ONLY HERE (api-contracts, `OD-DOC-004`).
      //
      // Absent, another company's and inactive are three DIFFERENT answers only where the domain has already
      // collapsed the dangerous distinction: `FindAssignableDepartmentByCodeAsync` returns null for both
      // "does not exist" and "belongs to another company", so `not_found` cannot be used to probe. `inactive`
      // is safe to name because the caller can already see that department.
      "Employee.DepartmentNotFound" => DepartmentNotFound,
      "Employee.DepartmentRequired" => DepartmentNotFound,
      "Employee.DepartmentInactive" => DepartmentInactive,
      "Employee.PositionNotFound" => PositionNotFound,
      "Employee.PositionRequired" => PositionNotFound,
      "Employee.PositionInactive" => PositionInactive,

      // ---- THE RUN AGGREGATES' OWN GUARDS (T-080). RULED FROM THE RAISE SITE, NOT FROM THE NAME.
      //
      // These reach the wire: `ImportEmployeesCommandHandler.cs:624-626` returns the factory's failure
      // directly and `ExportEmployeesQueryHandler.cs:174-177` does the same, so before these arms existed
      // a bad import key or file name answered `500 request.failed`.
      //
      // The split is by WHO SUPPLIED THE VALUE, which is the only question that decides whether a caller
      // can act on the answer.
      //
      // CALLER-SUPPLIED, so the caller can fix them by sending something else:
      "EmployeeImportRun.InvalidImportKey" => ApiErrors.RequestInvalid,
      "EmployeeImportRun.InvalidFileName" => ApiErrors.RequestInvalid,

      // ---- SERVER-COMPUTED, SO A 500 IS THE HONEST ANSWER. EXPLICIT DESPITE MATCHING THE DEFAULT.
      //
      // `InvalidCounts` guards `byteCount`, `rowCount`, `acceptedCount` and `rejectedCount`. Every one is
      // computed here, not sent: `byteCount` is `Encoding.UTF8.GetByteCount(content)`
      // (`EmployeeEndpointRouteBuilderExtensions.cs:716`) and the rest come from the parser. A negative
      // count is this pipeline's arithmetic error and there is nothing a caller could send differently.
      //
      // `InvalidColumnSet` guards `ExportEmployeesQueryHandler.Columns`, a `public static readonly` list
      // (`:95`), and `ExportEmployeesQuery` carries no column input at all — the caller cannot choose the
      // column set, so they cannot get it wrong.
      //
      // **Both are written out although the fallthrough already produces a 500.** The status is the same;
      // the difference is that it becomes a decision with a reason instead of an accident that happens to
      // read correctly.
      "EmployeeImportRun.InvalidCounts" => ApiErrors.WriteFailure,
      "EmployeeExportRun.InvalidColumnSet" => ApiErrors.WriteFailure,

      // ---- AND THIS ONE IS 500 FOR A REASON THAT RESTS ON TWO CONSTANTS AGREEING ACROSS A BOUNDARY.
      //
      // `EmployeeImportRun.IsValidActor` (`:218`) requires a non-blank actor of at most
      // `ActorMaximumLength` (256). The handler's own guard (`ImportEmployeesCommandHandler.cs:90-94`)
      // enforces only the non-blank half and answers `EmployeeErrors.InvalidActor` -> `Forbidden`, so it
      // does NOT subsume this one. The length half holds only because `AuthenticationSubject.Create`
      // caps a subject at 256 as well — a different module, an equal constant, and nothing asserting they
      // stay equal.
      //
      // So it is unreachable today and a 500 is right: the actor is issued by the platform, never chosen
      // by the caller, and a caller cannot act on the answer whatever it says. **If the subject limit ever
      // rises above 256 this becomes reachable and stays a 500 for the same reason** — which is why the
      // arm is worth writing even though nothing can currently hit it.
      "EmployeeImportRun.InvalidActor" => ApiErrors.WriteFailure,

      // ---- VALUE FAILURES. The row's own cells, each named so the operator knows which to fix.
      "Employee.InvalidEmployeeNumber" => ApiErrors.RequestInvalid,
      "Employee.InvalidNationalId" => ApiErrors.RequestInvalid,
      "Employee.InvalidFullName" => ApiErrors.RequestInvalid,
      "Employee.InvalidEmploymentDate" => ApiErrors.RequestInvalid,
      "EmployeeImport.EmploymentDateInvalid" => ApiErrors.RequestInvalid,
      "EmployeeImport.RowShapeInvalid" => ApiErrors.RequestInvalid,
      "EmployeeImport.DuplicateWithinFile" => ApiErrors.RequestInvalid,

      // ---- THE ONE ROW ERROR WITH NO SINGLE-CREATE COUNTERPART (`R9`, `OD-DOC-010`).
      //
      // A `POST` carrying a status is refused by the JSON contract's declared field set — `status` is not a
      // field there at all — so there is no employee-domain code to reuse. It is a rule of the IMPORT
      // CONTRACT about which columns a file may carry and what values they may hold, and it takes the
      // namespace the contract already opened for that class of failure.
      "EmployeeImport.StatusNotCreatable" => StatusNotCreatable,

      // ---- FILE-LEVEL FAILURES REPORTED AGAINST A ROW NUMBER.
      //
      // A bad header or an exceeded cap is reported at row 1 or row 0 rather than per row, because there is
      // no row to blame — the file as a whole is what was refused.
      "EmployeeImport.HeaderMissing" => ApiErrors.RequestInvalid,
      "EmployeeImport.HeaderColumnUnknown" => ApiErrors.RequestInvalid,
      "EmployeeImport.HeaderColumnMissing" => ApiErrors.RequestInvalid,
      "EmployeeImport.HeaderColumnDuplicated" => ApiErrors.RequestInvalid,
      "EmployeeImport.RowLimitExceeded" => ApiErrors.RequestInvalid,
      "EmployeeImport.ByteLimitExceeded" => ApiErrors.RequestInvalid,

      // ---- AND THE DEFAULT IS `request.invalid`, NOT A SERVER ERROR — the opposite of the route mapper's.
      //
      // The route mapper defaults to 500 because an unmapped code there means its table is out of date and a
      // 500 is visible. Here the report is already inside a `400`-shaped refusal of the FILE: the run was
      // refused, the operator is being told which rows to fix, and an unmapped row cause is still a problem
      // with that row. Reporting one row as a server failure inside an otherwise complete report would be
      // less true, not more.
      _ => ApiErrors.RequestInvalid
    };
  }

  // ================================================================================================
  // TWO STRINGS SPLIT IN T-096. ONE WIRE CODE WAS CARRYING TWO DIFFERENT FACTS.
  // ================================================================================================
  //
  // `department.not_found` and `position.not_found` were declared HERE at 400 and at
  // `DepartmentApiErrorMapper` / `PositionApiErrorMapper` at 404. **Same string, two statuses, and a caller
  // sees strings rather than constants.**
  //
  // ---- AND `DEC-L-079` DOES NOT FORCE THEM TO AGREE. IT EXPOSES THAT THEY ARE NOT THE SAME FACT.
  //
  //   404 on the resource route  the position RESOURCE does not exist
  //   400 on an import row       the position NAMED IN THIS ROW is invalid, and the row is the caller's
  //
  // Forcing one status would have made one of the two answers wrong. So the ROW case takes its own code,
  // following this file's own `employee_import.` convention — already used by `StatusNotCreatable` — and
  // the resource codes keep theirs.
  //
  // ---- THE TWO SIBLINGS KEEP THEIR STRINGS, AND THAT IS DELIBERATE.
  //
  // `department.inactive` and `position.inactive` are declared NOWHERE ELSE, so they carry one status and
  // one meaning. **Renaming them would change a wire contract to fix a defect they do not have** — the
  // split follows the collision, not the file.
  public static readonly ApiError DepartmentNotFound = new(400, "employee_import.department_not_found");

  public static readonly ApiError DepartmentInactive = new(400, "department.inactive");

  public static readonly ApiError PositionNotFound = new(400, "employee_import.position_not_found");

  public static readonly ApiError PositionInactive = new(400, "position.inactive");

  public static readonly ApiError StatusNotCreatable =
    new(400, "employee_import.status_not_creatable");
}
