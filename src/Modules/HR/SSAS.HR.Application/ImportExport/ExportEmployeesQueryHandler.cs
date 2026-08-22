using System.Globalization;
using System.Text;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Application.ImportExport;

// THE QUERY. It carries the SAME filter inputs the employee search carries and no others, which is
// `BRULE-DOC-0605` expressed as a type: an export is a search that leaves the building.
//
// There is no page number and no page size. A file with a page 2 is not a file — but the result is still
// bounded, by `Ceiling`, and an export that would exceed it is REFUSED rather than truncated.
public sealed record ExportEmployeesQuery(
  EmployeeScopeRequest? Scope = null,
  string? EmployeeNumber = null,
  IReadOnlyCollection<EmployeeStatus>? Statuses = null,
  Guid? DepartmentId = null,
  int? Ceiling = null);

// What an export produced: the bytes, the name to give them, and the identifier of the record saying it
// happened. The CONTENT is a string rather than a stream because the read is bounded — see the handler.
public sealed record EmployeeExportResult(
  Guid ExportRunId,
  string FileName,
  string Content,
  int RowCount,
  IReadOnlyList<string> ColumnSet);

// EXPORT EMPLOYEES TO CSV (FR-DOC-0201, FR-DOC-0202).
//
// ================================================================================================
// IT RUNS UNDER THE CALLER'S OWN MATERIALIZED EMPLOYEE READ SCOPE. THAT IS SETTLED.
// ================================================================================================
//
// The scope is resolved live, by the same resolver every other employee read uses, and the rows come back
// through the same scoped predicate. An export cannot see an employee the caller could not have listed —
// which means `BRULE-DOC-0605` is inherited rather than re-implemented, and there is no code path here that
// could widen it.
//
// ---- TWO PERMISSIONS, AND THE SECOND ONE IS NOT REDUNDANT.
//
// `HR.Employees.Export` is checked here, and the read scope's own resolver checks `HR.Employees.View`. Both
// are required, and `OD-DOC-005`'s "granted independently" is about neither IMPLYING the other rather than
// about either being sufficient alone: an export reads employee data, so the read authority is a floor, and
// the export authority is the additional, deliberately-granted permission on top of it. See `DEC-DOC-0015`.
//
// ---- A FAILED EXPORT WRITES NO RUN RECORD, AND THAT IS THE OPPOSITE OF THE IMPORT RULE.
//
// An import run records an ATTEMPT TO WRITE, so even a refusal is worth recording and consumes its key. An
// export run records BYTES HAVING LEFT. A failed export has nothing to disclose, so a record of it would be
// an audit trail of a non-event — and would dilute the one signal this table exists to carry.
public sealed class ExportEmployeesQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeReadService employees,
  IEmployeeExportRunRepository runs,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentCompany currentCompany,
  ICurrentUser currentUser,
  IDateTimeProvider clock,
  EmployeeImportLimits? limits = null)
{
  // ---- THE COLUMN SET, IN ORDER, AND `nationalId` IS NOT IN IT (OD-DOC-006).
  //
  // Unconditionally: there is no permission, parameter or caller for which the column appears. It is not
  // filtered out here — `EmployeeExportRow` has no such property to filter — so this list is a statement of
  // what the projection already cannot carry rather than a gate that could be opened.
  //
  // ================================================================================================
  // `status` IS SPECIFIED HERE AND IS **NOT** AN IMPORT COLUMN. THE ROUND TRIP DOES NOT CLOSE.
  // ================================================================================================
  //
  // `api-contracts.md` lists these six for the export. The import contract declares five required columns
  // plus optional `nationalId`, and `AC-DOC-0002` states plainly that **status is not a column** — creation
  // produces `Active`, and an import cannot create a terminated employee. Meanwhile `DEC-DOC-0008` and
  // `AC-DOC-0016` require that an exported file, edited and re-submitted, is a legal import.
  //
  // Those three cannot all be true. The import refuses UNKNOWN columns by design (`DEC-DOC-0002`), so a file
  // this handler produces is refused for its `status` column — `HeaderColumnUnknown`, before a single row is
  // read.
  //
  // **This is reported, not resolved.** The package reasons about the round trip only for `nationalId`, and
  // choosing between "drop `status` from exports", "declare it an ignored import column" and "narrow the
  // round-trip claim" is an owner decision about what an export is FOR — see `OD-DOC-010`. The contract is
  // implemented exactly as written, and the gap has a test that demonstrates it rather than a comment that
  // describes it.
  public static readonly IReadOnlyList<string> Columns =
  [
    EmployeeImportColumns.EmployeeNumber,
    EmployeeImportColumns.FullName,
    EmployeeImportColumns.EmploymentDate,
    EmployeeImportColumns.DepartmentCode,
    EmployeeImportColumns.PositionCode,
    "status"
  ];

  private readonly EmployeeImportLimits limits = limits ?? EmployeeImportLimits.Default;

  public async Task<Result<EmployeeExportResult>> HandleAsync(
    ExportEmployeesQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (currentTenant.TenantId is not { } tenantId ||
      currentCompany.CompanyId is not { } companyId ||
      string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<EmployeeExportResult>(EmployeeErrors.InvalidActor);
    }

    // ---- THE EXPORT PERMISSION, CHECKED BEFORE ANY ROW IS READ.
    //
    // Checked here rather than only at the endpoint, so the operation is not authorized by whichever caller
    // happens to reach it — the same argument `ChangeEmployeeDepartmentCommandHandler` makes for its own
    // functional check, and the same mechanism.
    if (!currentUser.Permissions.Contains(HrPermissionNames.ExportEmployees, StringComparer.Ordinal))
    {
      return Result.Failure<EmployeeExportResult>(EmployeeErrors.ReadPermissionDenied);
    }

    var ceiling = query.Ceiling ?? limits.MaximumRows;
    if (ceiling < 1)
    {
      return Result.Failure<EmployeeExportResult>(EmployeeErrors.InvalidPagination);
    }

    var scope = await scopeResolver.ResolveAsync(
      query.Scope ?? new EmployeeScopeRequest(), cancellationToken);
    if (scope.IsFailure)
    {
      return Result.Failure<EmployeeExportResult>(scope.Error);
    }

    // The SAME criteria type the search takes. `PageNumber`/`PageSize` are left at their defaults and
    // ignored by the export read — passing the ceiling as a page size would make an export look like a page,
    // and the two are bounded for different reasons.
    var criteria = new EmployeeSearchCriteria(
      EmployeeNumber: query.EmployeeNumber,
      Statuses: query.Statuses,
      DepartmentId: query.DepartmentId);

    var rows = await employees.ExportEmployeesAsync(scope.Value, criteria, ceiling, cancellationToken);

    // ---- OVER THE CEILING IS A REFUSAL, NEVER A TRUNCATION.
    //
    // The read asked for one row past the limit precisely so this can be told apart from a result that
    // exactly fills it. Returning the first N of a larger set would hand the operator a file that looks
    // complete, and an operator who believes they have everybody is worse off than one who was refused.
    if (rows.Count > ceiling)
    {
      return Result.Failure<EmployeeExportResult>(EmployeeImportErrors.RowLimitExceeded);
    }

    var content = Write(rows);
    var executedUtc = clock.UtcNow;

    var run = EmployeeExportRun.Completed(
      tenantId,
      companyId,
      rows.Count,
      Columns,
      scope.Value.Companies.CompanyIds,
      scope.Value.Branches.BranchIds,
      executedUtc,
      currentUser.UserId!);
    if (run.IsFailure)
    {
      return Result.Failure<EmployeeExportResult>(run.Error);
    }

    await runs.AddAsync(run.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<EmployeeExportResult>(saved.Error);
    }

    return Result.Success(new EmployeeExportResult(
      run.Value.Id, FileNameFor(executedUtc), content, rows.Count, Columns));
  }

  // ---- THE FILE NAME IS SERVER-GENERATED, AND NO CALLER INPUT REACHES IT.
  //
  // Reflecting a caller-supplied name into a `Content-Disposition` header is a header-injection surface for
  // no benefit. The timestamp makes successive exports distinguishable in a downloads folder, which is the
  // only thing the name has to do.
  public static string FileNameFor(DateTimeOffset executedUtc) =>
    string.Create(
      CultureInfo.InvariantCulture,
      $"employees-{executedUtc.UtcDateTime:yyyyMMdd-HHmmss}.csv");

  // ---- THE WRITER (DEC-DOC-0008).
  //
  // BUFFERED, NOT STREAMED, and that is a consequence of the ceiling rather than a shortcut: everything a
  // caller can ask for is bounded above by `limits.MaximumRows`, so the whole file fits in memory by
  // construction. A streamed response would also have to begin before the run record could be written, and
  // a partial write that failed halfway would have disclosed rows the audit trail never recorded.
  //
  // The date format is `yyyy-MM-dd`, exactly what the import parses. A culture-dependent format would
  // produce a file that re-imports as a different date or not at all, which is the round-trip property
  // failing quietly.
  internal static string Write(IReadOnlyList<EmployeeExportRow> rows)
  {
    var builder = new StringBuilder();

    builder.Append(string.Join(',', Columns)).Append('\n');

    foreach (var row in rows)
    {
      builder
        .Append(Escape(row.EmployeeNumber)).Append(',')
        .Append(Escape(row.FullName)).Append(',')
        .Append(row.EmploymentDate.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        .Append(',')
        .Append(Escape(row.DepartmentCode)).Append(',')
        .Append(Escape(row.PositionCode)).Append(',')
        .Append(row.Status.ToString())
        .Append('\n');
    }

    return builder.ToString();
  }

  // Quoting is applied only where it is needed, and a quote inside a quoted field is doubled — RFC 4180, and
  // exactly what the import parser reads back.
  private static string Escape(string value) =>
    value.Contains(',', StringComparison.Ordinal) ||
    value.Contains('"', StringComparison.Ordinal) ||
    value.Contains('\n', StringComparison.Ordinal) ||
    value.Contains('\r', StringComparison.Ordinal)
      ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
      : value;
}
