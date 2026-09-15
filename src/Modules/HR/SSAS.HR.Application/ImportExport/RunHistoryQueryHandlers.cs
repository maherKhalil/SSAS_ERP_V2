using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Application.Employees.Reads;

namespace SSAS.HR.Application.ImportExport;

// READ THE IMPORT RUN HISTORY (FR-DOC-0103).
//
// The durable record of imports for the caller's company: who, when, the counts, the outcome. Append-only,
// and scoped like every other read.
//
// ---- THE TWO HANDLERS ARE SIBLINGS, NOT ONE GENERIC HISTORY HANDLER.
//
// They differ in what they return, and the difference is load-bearing: the export listing deliberately omits
// the scope snapshot (`DEC-DOC-0016`) while the import listing has none to omit. One handler over a shared
// shape would have made that omission a runtime branch instead of a property of two types — and a branch is
// something a later change can take.
public sealed record SearchImportRunsQuery(
  int PageNumber = EmployeeRunHistoryCriteria.DefaultPageNumber,
  int PageSize = EmployeeRunHistoryCriteria.DefaultPageSize,
  EmployeeScopeRequest? Scope = null);

public sealed class SearchImportRunsQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeRunHistoryReadService history)
{
  public async Task<Result<PagedResult<EmployeeImportRunListItem>>> HandleAsync(
    SearchImportRunsQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    // PAGINATION IS REFUSED, NOT CLAMPED — the rule the employee and department searches already apply.
    if (query.PageNumber < 1)
    {
      return Result.Failure<PagedResult<EmployeeImportRunListItem>>(EmployeeErrors.InvalidPageNumber);
    }

    if (query.PageSize < 1 || query.PageSize > EmployeeRunHistoryCriteria.MaxPageSize)
    {
      return Result.Failure<PagedResult<EmployeeImportRunListItem>>(EmployeeErrors.InvalidPageSize);
    }

    // The scope is resolved LIVE by the same resolver every employee read uses, so `HR.Employees.View` is
    // checked here on exactly the terms it is checked everywhere else — and nothing is cached from an
    // earlier call in the same request.
    var scope = await scopeResolver.ResolveAsync(
      query.Scope ?? new EmployeeScopeRequest(), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PagedResult<EmployeeImportRunListItem>>(scope.Error)
      : Result.Success(await history.SearchImportRunsAsync(
        scope.Value, new EmployeeRunHistoryCriteria(query.PageNumber, query.PageSize), cancellationToken));
  }
}

// READ THE EXPORT RUN HISTORY (FR-DOC-0202).
//
// `requirements.md` calls this *"the same record from the other direction, and the more important one: an
// export is the only operation in the module that moves data outside the system's control, so the record of
// what column set left is the control that survives it."*
//
// `ColumnSet` therefore ships. The scope snapshot does not — see `DEC-DOC-0016` and the read model.
public sealed record SearchExportRunsQuery(
  int PageNumber = EmployeeRunHistoryCriteria.DefaultPageNumber,
  int PageSize = EmployeeRunHistoryCriteria.DefaultPageSize,
  EmployeeScopeRequest? Scope = null);

public sealed class SearchExportRunsQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeRunHistoryReadService history)
{
  public async Task<Result<PagedResult<EmployeeExportRunListItem>>> HandleAsync(
    SearchExportRunsQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    if (query.PageNumber < 1)
    {
      return Result.Failure<PagedResult<EmployeeExportRunListItem>>(EmployeeErrors.InvalidPageNumber);
    }

    if (query.PageSize < 1 || query.PageSize > EmployeeRunHistoryCriteria.MaxPageSize)
    {
      return Result.Failure<PagedResult<EmployeeExportRunListItem>>(EmployeeErrors.InvalidPageSize);
    }

    // ---- `HR.Employees.View`, NOT `HR.Employees.Export`.
    //
    // Reading the record that an export happened is an employee read; PERFORMING one is the separately
    // granted capability (`OD-DOC-005`, `DEC-DOC-0015`). Gating the history on `Export` would mean the
    // people who audit extractions must also be able to perform them, which is the opposite of what
    // separating the permission was for.
    var scope = await scopeResolver.ResolveAsync(
      query.Scope ?? new EmployeeScopeRequest(), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PagedResult<EmployeeExportRunListItem>>(scope.Error)
      : Result.Success(await history.SearchExportRunsAsync(
        scope.Value, new EmployeeRunHistoryCriteria(query.PageNumber, query.PageSize), cancellationToken));
  }
}
