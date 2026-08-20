using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Departments;

// ASSIGN OR REPLACE THE DEPARTMENT'S MANAGER (REQ-HR-0102).
//
// ---- ONE COMMAND FOR BOTH, BECAUSE THEY ARE THE SAME INTENT.
//
// "This department is headed by this person" is true whether or not somebody held the role before. Splitting
// assign from replace would make the caller ask a question they should not have to — is there a manager
// already? — and would open a window between clearing and assigning where the department has none.
public sealed record AssignDepartmentManagerCommand(
  Guid DepartmentId,
  Guid EmployeeId,
  byte[] RowVersion);

public sealed record ClearDepartmentManagerCommand(
  Guid DepartmentId,
  byte[] RowVersion);

public sealed class AssignDepartmentManagerCommandHandler(
  IDepartmentRepository departments,
  IEmployeeRepository employees,
  IDepartmentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    AssignDepartmentManagerCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(DepartmentErrors.InvalidActor);
    }

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.UpdateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var department = loaded.Value;

    // ================================================================================================
    // WHAT MAKES AN EMPLOYEE ELIGIBLE — AND WHAT DELIBERATELY DOES NOT.
    // ================================================================================================
    //
    // Same tenant, same company, not terminated. That is the whole list.
    //
    // BRANCH IS NOT CONSULTED. A department spans the branches of its company, so requiring the manager to
    // work at any particular branch would name one of several arbitrarily.
    //
    // DEPARTMENT MEMBERSHIP IS NOT CONSULTED EITHER, in either direction: the employee may belong to this
    // department, to another department in the same company, or — until Phase 3 — to none, and all three are
    // eligible. `Employee.DepartmentId == Department.Id` is explicitly NOT a rule.
    //
    // AND THIS IS NOT A REPORTING LINE. Heading a department is not the same relationship as managing a
    // person; `BR-HR-0007` presumes an employee-to-manager link that no authority defines, and it remains
    // deferred rather than being quietly satisfied by this.
    var employee = await employees.GetByIdAsync(command.EmployeeId, cancellationToken);
    if (employee is null || employee.TenantId != tenantId)
    {
      return Result.Failure(DepartmentErrors.ManagerEmployeeNotFound);
    }

    if (employee.CompanyId != department.CompanyId)
    {
      return Result.Failure(DepartmentErrors.ManagerInDifferentCompany);
    }

    if (employee.Status == EmployeeStatus.Terminated)
    {
      return Result.Failure(DepartmentErrors.ManagerTerminated);
    }

    var existing = await departments.GetManagerAsync(department.Id, cancellationToken);
    if (existing is null)
    {
      var assigned = DepartmentManager.Assign(
        department.Id, tenantId, department.CompanyId, command.EmployeeId,
        currentUser.UserId!, clock.UtcNow);
      if (assigned.IsFailure)
      {
        return Result.Failure(assigned.Error);
      }

      await departments.SetManagerAsync(assigned.Value, cancellationToken);
    }
    else
    {
      // ---- REPLACEMENT MUTATES THE EXISTING ROW.
      //
      // Its own RowVersion is the concurrency token, so two callers replacing from the same read cannot
      // both succeed — the second's token no longer matches and the database refuses the update. The
      // primary key on DepartmentId makes a second row unrepresentable regardless.
      var reassigned = existing.ReassignTo(command.EmployeeId, currentUser.UserId!, clock.UtcNow);
      if (reassigned.IsFailure)
      {
        return reassigned;
      }
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

// CLEAR THE DEPARTMENT'S MANAGER.
//
// ---- REMOVING THE ASSOCIATION IS NOT DELETING ANYTHING THE NO-DELETE RULE PROTECTS.
//
// `BRULE-DEP-0016` governs DEPARTMENTS. This row is a current-state association, and its absence is exactly
// what "this department has no manager" means. It is not history, and the append-only rules that govern
// `EmployeeDepartmentAssignment` do not apply to it — if manager history is ever required it will be a
// separate append-only log, at which point this operation stops removing anything.
public sealed class ClearDepartmentManagerCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant)
{
  public async Task<Result> HandleAsync(
    ClearDepartmentManagerCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.UpdateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var existing = await departments.GetManagerAsync(loaded.Value.Id, cancellationToken);
    if (existing is null)
    {
      // A named refusal rather than a silent success: a caller who believes they removed a manager that was
      // never there has a different picture of the system than the system does.
      return Result.Failure(DepartmentErrors.ManagerNotAssigned);
    }

    await departments.ClearManagerAsync(existing, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}
