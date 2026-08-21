using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// READ ONE EMPLOYEE'S POSITION HISTORY (FR-POS-0212, DEC-POS-0008).
public sealed record GetEmployeePositionHistoryQuery(Guid EmployeeId, EmployeeScopeRequest? Scope = null);

// ---- THE HISTORY IS SCOPED BY ITS EMPLOYEE, exactly as the branch history is.
//
// The assignment rows are company-owned but not branch-owned, so there is no branch predicate to write over
// them. The read service therefore proves the EMPLOYEE is inside the resolved scope before loading anything,
// and returns null when it is not — the same NotFound the employee read gives. That ordering is the whole
// control: a caller confined to one branch must not be able to name an employee identifier and learn every
// position that employee has ever held.
//
// ---- IT ADDS NO AUTHORIZATION DIMENSION (FR-POS-0212, DEC-POS-0020).
//
// `HR.Employees.View` and the caller's existing employee scope, and nothing else. Reading someone's
// promotion history is a read of that person's own record; it is emphatically not a position permission,
// because the rows describe an EMPLOYEE rather than the job catalog.
//
// ---- WHY THIS ARRIVED IN PHASE 4 RATHER THAN WITH THE WRITES IN PHASE 3.
//
// A phase-plan omission, recorded rather than smoothed over: Phase 3 built the column, the append-only log
// and `ChangePosition`, and the read path that exposes them was not in its slice list. Phase 4's route
// reconciliation found nineteen handlers against twenty specified routes, which is exactly what that
// reconciliation step exists to catch.
public sealed class GetEmployeePositionHistoryQueryHandler(
  IEmployeeScopeResolver scopeResolver,
  IEmployeeReadService employees)
{
  public async Task<Result<IReadOnlyList<EmployeePositionHistoryEntry>>> HandleAsync(
    GetEmployeePositionHistoryQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var request = query.Scope ?? new EmployeeScopeRequest();
    if (request.CompanyScope != EmployeeCompanyScopeMode.CurrentCompany)
    {
      return Result.Failure<IReadOnlyList<EmployeePositionHistoryEntry>>(EmployeeErrors.InvalidReadScope);
    }

    var scope = await scopeResolver.ResolveAsync(request, cancellationToken);
    if (scope.IsFailure)
    {
      return Result.Failure<IReadOnlyList<EmployeePositionHistoryEntry>>(scope.Error);
    }

    var history = await employees.GetEmployeePositionHistoryAsync(
      scope.Value, query.EmployeeId, cancellationToken);

    return history is null
      ? Result.Failure<IReadOnlyList<EmployeePositionHistoryEntry>>(EmployeeErrors.NotFound)
      : Result.Success(history);
  }
}
