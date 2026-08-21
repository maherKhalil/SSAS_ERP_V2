using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// SEARCH EMPLOYEES (REQ-HR-0005).
//
// The only read that may span companies, and then only across the caller's OWN authorized set.
public sealed record SearchEmployeesQuery(
  EmployeeScopeRequest? Scope = null,
  int PageNumber = EmployeeSearchCriteria.DefaultPageNumber,
  int PageSize = EmployeeSearchCriteria.DefaultPageSize,
  string? EmployeeNumber = null,
  IReadOnlyCollection<EmployeeStatus>? Statuses = null,
  // Optional, and narrowing only — see the note on EmployeeSearchCriteria.DepartmentId.
  Guid? DepartmentId = null);

public sealed class SearchEmployeesQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeReadService employees)
{
  public async Task<Result<PagedResult<EmployeeSummary>>> HandleAsync(
    SearchEmployeesQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    // ---- PAGINATION IS REFUSED, NOT CLAMPED.
    //
    // Silently reducing a page size of 5000 to 200 would return a page the caller did not ask for and let
    // them believe they had seen the rest. An out-of-range request is a malformed request.
    if (query.PageNumber < 1 ||
      query.PageSize < 1 ||
      query.PageSize > EmployeeSearchCriteria.MaxPageSize)
    {
      return Result.Failure<PagedResult<EmployeeSummary>>(EmployeeErrors.InvalidPagination);
    }

    var scope = await scopeResolver.ResolveAsync(query.Scope ?? new EmployeeScopeRequest(), cancellationToken);
    if (scope.IsFailure)
    {
      return Result.Failure<PagedResult<EmployeeSummary>>(scope.Error);
    }

    var criteria = new EmployeeSearchCriteria(
      query.PageNumber,
      query.PageSize,
      query.EmployeeNumber,
      query.Statuses,
      query.DepartmentId);

    var page = await employees.SearchEmployeesAsync(scope.Value, criteria, cancellationToken);

    return Result.Success(page);
  }
}
