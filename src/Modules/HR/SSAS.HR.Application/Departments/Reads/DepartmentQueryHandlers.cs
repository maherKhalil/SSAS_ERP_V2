using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments.Reads;

// The bounds a department search must stay inside. Stated as constants rather than inline so the guard and
// the refusal cannot drift apart.
public static class DepartmentSearchCriteria
{
  public const int DefaultPageNumber = 1;

  public const int DefaultPageSize = 25;

  // A ceiling, not a clamp. See the refusal below.
  public const int MaxPageSize = 200;
}

public sealed record GetDepartmentQuery(Guid DepartmentId);

public sealed record GetDepartmentChildrenQuery(Guid DepartmentId);

// EVERY QUERY RESOLVES A SCOPE FIRST, AND CANNOT REACH THE DATA WITHOUT ONE.
//
// ================================================================================================
// THE MANAGER IS RESOLVED THROUGH THE EMPLOYEE SCOPE, NOT THE DEPARTMENT ONE.
// ================================================================================================
//
// Two scopes, because there are two kinds of fact here. WHETHER a department has a manager is a department
// fact and rides the department's company scope. WHO that manager is is an employee fact, and employees are
// branch-scoped — so a caller authorized only for Riyadh reading a company-wide department whose manager
// works in Jeddah gets "somebody is assigned" and no name.
//
// This uses the EXISTING employee read scope rather than inventing a second branch authorization model.
// The failure it prevents is subtle and would have been easy to ship: a join inside the department read
// would have disclosed the manager's identity on the strength of department visibility alone.
public sealed class GetDepartmentQueryHandler(
  IDepartmentScopeResolver scopeResolver,
  IDepartmentReadService departments,
  Employees.Reads.IEmployeeScopeResolver employeeScopeResolver,
  Employees.Reads.IEmployeeReadService employees)
{
  public async Task<Result<DepartmentDetail>> HandleAsync(
    GetDepartmentQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var scope = await scopeResolver.ResolveAsync(new DepartmentScopeRequest(), cancellationToken);
    if (scope.IsFailure)
    {
      return Result.Failure<DepartmentDetail>(scope.Error);
    }

    var department = await departments.GetAsync(scope.Value, query.DepartmentId, cancellationToken);
    if (department.IsFailure)
    {
      return department;
    }

    if (department.Value.ManagerEmployeeId is not { } managerId)
    {
      // No association at all. `Manager` stays null, which is a different answer from "assigned but
      // undisclosed" and must not be confused with it.
      return department;
    }

    var summary = await ResolveManagerAsync(managerId, cancellationToken);

    return Result.Success(department.Value with { Manager = summary });
  }

  private async Task<DepartmentManagerSummary> ResolveManagerAsync(
    Guid managerEmployeeId, CancellationToken cancellationToken)
  {
    // ALL AUTHORIZED BRANCHES, because the question is "may this caller see this person at all", not "is
    // this person in the branch the caller happens to be acting in". A manager legitimately sits outside
    // the current branch while still being visible to a caller authorized for several.
    var employeeScope = await employeeScopeResolver.ResolveAsync(
      new Employees.Reads.EmployeeScopeRequest(
        Employees.Reads.EmployeeCompanyScopeMode.AllAuthorizedCompanies,
        Employees.Reads.EmployeeBranchScopeMode.AllAuthorizedBranches),
      cancellationToken);

    // A caller without HR.Employees.View, or with no branch scope at all, learns that a manager exists and
    // nothing more. That is the correct answer rather than an error: the DEPARTMENT read succeeded.
    if (employeeScope.IsFailure)
    {
      return DepartmentManagerSummary.Undisclosed();
    }

    var employee = await employees.GetEmployeeAsync(
      employeeScope.Value, managerEmployeeId, cancellationToken);

    if (employee is null)
    {
      // Outside the caller's employee scope. Indistinguishable, deliberately, from an employee who does
      // not exist — so this cannot be used to probe for employees in branches the caller cannot reach.
      return DepartmentManagerSummary.Undisclosed();
    }

    return new DepartmentManagerSummary(
      IsAssigned: true,
      employee.EmployeeId,
      employee.EmployeeNumber,
      employee.FullName,
      // A terminated employee is never presented as an active manager. The association is not cleared on
      // termination — that would destroy the record there had been one — so the distinction is made here.
      IsActive: employee.Status != SSAS.HR.Domain.Employees.EmployeeStatus.Terminated);
  }
}

public sealed class SearchDepartmentsQueryHandler(
  IDepartmentScopeResolver scopeResolver,
  IDepartmentReadService departments)
{
  public async Task<Result<PagedResult<DepartmentListItem>>> HandleAsync(
    SearchDepartmentsQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    // ---- PAGINATION IS REFUSED, NOT CLAMPED.
    //
    // Silently reducing a page size of 5000 to 200 would return a page the caller did not ask for and let
    // them believe they had seen the rest. An out-of-range request is a malformed request — the same rule
    // the employee search already applies.
    if (query.Page < 1 || query.PageSize < 1 || query.PageSize > DepartmentSearchCriteria.MaxPageSize)
    {
      return Result.Failure<PagedResult<DepartmentListItem>>(DepartmentErrors.InvalidPagination);
    }

    var scope = await scopeResolver.ResolveAsync(
      new DepartmentScopeRequest(query.CompanyScope), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PagedResult<DepartmentListItem>>(scope.Error)
      : await departments.SearchAsync(scope.Value, query, cancellationToken);
  }
}

// REQ-HR-0101. ONE LEVEL, DELIBERATELY.
//
// The approved package specifies the adjacency model and no full-tree contract, so this returns the direct
// children and nothing deeper. A caller wanting a whole tree walks it, which makes the cost of the depth
// visible to whoever is paying it rather than hidden inside one expensive call.
public sealed class GetDepartmentChildrenQueryHandler(
  IDepartmentScopeResolver scopeResolver,
  IDepartmentReadService departments)
{
  public async Task<Result<IReadOnlyList<DepartmentChild>>> HandleAsync(
    GetDepartmentChildrenQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var scope = await scopeResolver.ResolveAsync(new DepartmentScopeRequest(), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<IReadOnlyList<DepartmentChild>>(scope.Error)
      : await departments.GetChildrenAsync(scope.Value, query.DepartmentId, cancellationToken);
  }
}
