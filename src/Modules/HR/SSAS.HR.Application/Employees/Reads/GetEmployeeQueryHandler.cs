using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// READ ONE EMPLOYEE (REQ-HR-0006).
//
// THE QUERY CARRIES NO SCOPE. There is no TenantId, no CompanyId and no BranchId on it: the caller states
// WHICH employee and, at most, which of their own authorized scopes to look in. What that scope contains is
// resolved from the trusted execution context every time.
public sealed record GetEmployeeQuery(Guid EmployeeId, EmployeeScopeRequest? Scope = null);

// The handler AUTHORIZES BY DELEGATION and then reads. It contains no permission check, no company rule and
// no branch rule of its own — duplicating any of them here is how a read path and a write path drift apart
// (FP-006C4). Its whole job is: resolve a scope, or fail; then read within it.
public sealed class GetEmployeeQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeReadService employees)
{
  public async Task<Result<EmployeeDetail>> HandleAsync(
    GetEmployeeQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    // Single-employee reads are CurrentCompany only in Milestone 1: AllAuthorizedCompanies exists to make a
    // cross-company SEARCH meaningful, and an identifier lookup has no such need.
    var request = query.Scope ?? new EmployeeScopeRequest();
    if (request.CompanyScope != EmployeeCompanyScopeMode.CurrentCompany)
    {
      return Result.Failure<EmployeeDetail>(EmployeeErrors.InvalidReadScope);
    }

    var scope = await scopeResolver.ResolveAsync(request, cancellationToken);
    if (scope.IsFailure)
    {
      return Result.Failure<EmployeeDetail>(scope.Error);
    }

    var employee = await employees.GetEmployeeAsync(scope.Value, query.EmployeeId, cancellationToken);

    // OUT OF SCOPE IS INDISTINGUISHABLE FROM NONEXISTENT. Both are NotFound, so the read cannot be used to
    // test whether an identifier exists in a company or branch the caller cannot reach.
    return employee is null
      ? Result.Failure<EmployeeDetail>(EmployeeErrors.NotFound)
      : Result.Success(employee);
  }
}
