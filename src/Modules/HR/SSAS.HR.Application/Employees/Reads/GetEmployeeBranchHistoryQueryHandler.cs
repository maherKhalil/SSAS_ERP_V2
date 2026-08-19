using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// READ ONE EMPLOYEE'S BRANCH HISTORY (REQ-HR-0007, ADR-024 decision 6).
public sealed record GetEmployeeBranchHistoryQuery(Guid EmployeeId, EmployeeScopeRequest? Scope = null);

// ---- THE HISTORY IS SCOPED BY ITS EMPLOYEE.
//
// The assignment rows are not branch-owned, so there is no branch predicate to write over them. The read
// service therefore proves the EMPLOYEE is inside the resolved scope before loading anything, and returns
// null when it is not — the same NotFound the employee read gives. That ordering is the whole control: a
// caller confined to one branch must not be able to name an employee identifier and learn every branch that
// employee has ever worked in.
public sealed class GetEmployeeBranchHistoryQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeReadService employees)
{
  public async Task<Result<IReadOnlyList<EmployeeBranchHistoryEntry>>> HandleAsync(
    GetEmployeeBranchHistoryQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var request = query.Scope ?? new EmployeeScopeRequest();
    if (request.CompanyScope != EmployeeCompanyScopeMode.CurrentCompany)
    {
      return Result.Failure<IReadOnlyList<EmployeeBranchHistoryEntry>>(EmployeeErrors.InvalidReadScope);
    }

    var scope = await scopeResolver.ResolveAsync(request, cancellationToken);
    if (scope.IsFailure)
    {
      return Result.Failure<IReadOnlyList<EmployeeBranchHistoryEntry>>(scope.Error);
    }

    var history = await employees.GetEmployeeBranchHistoryAsync(
      scope.Value, query.EmployeeId, cancellationToken);

    return history is null
      ? Result.Failure<IReadOnlyList<EmployeeBranchHistoryEntry>>(EmployeeErrors.NotFound)
      : Result.Success(history);
  }
}
