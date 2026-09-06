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
    // Same tenant, same company, not terminated, and NOT A MEMBER OF THIS DEPARTMENT. That is the whole
    // list. *(The fourth was added 2026-09-06; see the superseded block below for why it was not there.)*
    //
    // BRANCH IS NOT CONSULTED. A department spans the branches of its company, so requiring the manager to
    // work at any particular branch would name one of several arbitrarily.
    //
    // ---- ⚠⚠⚠ DEPARTMENT MEMBERSHIP *IS* CONSULTED, AS OF THE OWNER'S RULING. SUPERSEDED 2026-09-06.
    //
    // THIS BLOCK PREVIOUSLY READ: *"DEPARTMENT MEMBERSHIP IS NOT CONSULTED EITHER, in either direction…
    // `Employee.DepartmentId == Department.Id` is explicitly NOT a rule"*, and argued the position below —
    // that heading a department is not a reporting line, that `BR-HR-0007` presumes an employee-to-manager
    // link no authority defines, and that it should stay deferred rather than be quietly satisfied here.
    //
    // ***THAT ARGUMENT IS READING (ii)-ONLY, AND IT WAS PUT TO THE OWNER AND NOT CHOSEN.*** `README.md`
    // named it in advance — *"If the owner's intent is (ii) only, say so… a legitimate answer but must be
    // recorded rather than assumed"* — and on 2026-08-20 the owner CLOSED `OD-DEP-003` adopting reading
    // (iii) (`decisions-approved.md:27`), which is *"(i) now, (ii) when a reporting line is introduced"*.
    // **Reading (i) is *"an employee may not be the manager of the department they themselves belong to"*,
    // marked enforceable in FP-007 — Yes, fully.**
    //
    // ⚠ THE ARGUMENT IS KEPT RATHER THAN DELETED BECAUSE IT WAS NOT WRONG WHEN WRITTEN. This comment and the
    // code it described were one commit, `245f64b` 2026-08-20 16:18; the ruling reached the repository at
    // `4a84e7d` 2026-08-21 05:03, and the file was never reopened. *It went stale, it did not dissent.*
    //
    // ⚠⚠ (ii) REMAINS DEFERRED AND THAT HALF OF THE OLD ARGUMENT STILL HOLDS: heading a department is still
    // not a reporting line, and no authority defines one. The ruling transfers (ii) to whichever package
    // introduces it. Only (i) is enforced here.
    //
    // ⚠⚠⚠ AND ONLY THE ASSIGN ROUTE IS CLOSED. Reading (i) is a STATE INVARIANT with two routes into it, and
    // MOVING an employee into the department they manage is the other one — unenforced, deferred by the
    // owner pending a question about existing data. See the note in `DepartmentEndpointTests`.
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

    // `OD-DEP-003` reading (i). Checked HERE rather than in `DepartmentManager.Assign` because the factory is
    // never handed the employee's own `DepartmentId` — its parameters are department, tenant, company,
    // employee, actor and timestamp — so it could not express this invariant without a signature change.
    // ⚠ THE BYPASS THAT BUYS: anything constructing `DepartmentManager` without coming through this handler
    // is unaffected by this line. Today nothing does, and the repository surface is asserted closed, but a
    // future second write path would need its own check or this rule moves into the domain.
    if (employee.DepartmentId == department.Id)
    {
      return Result.Failure(DepartmentErrors.ManagerInOwnDepartment);
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
