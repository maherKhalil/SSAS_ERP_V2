using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// CHANGE AN EMPLOYEE'S DEPARTMENT (REQ-HR-0102, ADR-026).
//
// THE COMMAND CARRIES A DESTINATION, NOT A SOURCE — the same rule the branch transfer follows. The source
// is the employee's current department, read from the record; accepting one from the caller would let a
// request assert where a record used to be.
public sealed record ChangeEmployeeDepartmentCommand(
  Guid EmployeeId,
  Guid DestinationDepartmentId,
  byte[] ExpectedRowVersion,
  string? ReasonCode = null,
  string? ReasonText = null);

// ================================================================================================
// WHY THIS IS AN UPDATE AND NOT A TRANSFER.
// ================================================================================================
//
// A branch transfer moves a record across a SECURITY PARTITION: the destination branch decides who may
// subsequently see the employee, which is why it holds its own permission, its own access resolver and a
// dedicated write channel that re-proves the whole declaration at commit.
//
// A department change moves nothing across any partition. The employee stays in the same tenant, the same
// company and the same branch; only their place in the org structure changes. So this operation takes
// `HR.Employees.Update` and opens no channel — there is no second security dimension for one to protect.
//
// Adding `HR.Employees.ChangeDepartment` would imply an authority boundary that does not exist, and a
// permission that authorizes nothing distinct is worse than none (the same reasoning that left
// `HR.Departments.Delete` uncreated).
//
// ---- BRANCH IS ABSENT FROM THIS FILE, AND THAT IS THE ENFORCEMENT.
//
// There is no BranchId assignment to guard and no branch rule to check, because a department says nothing
// about a branch. §14's regression proof holds for the same structural reason.
public sealed class ChangeEmployeeDepartmentCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentCompany currentCompany,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ChangeEmployeeDepartmentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is null ||
      currentCompany.CompanyId is not { } companyId ||
      string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    // ---- 1. FUNCTIONAL AUTHORITY, BEFORE ANYTHING IS LOADED OR REVEALED.
    //
    // Checked here rather than only at the endpoint, so the operation is not authorized by whichever
    // caller happens to reach it. It is the same mechanism the department scope resolver uses.
    if (!currentUser.Permissions.Contains(HrPermissionNames.UpdateEmployees, StringComparer.Ordinal))
    {
      return Result.Failure(EmployeeErrors.WritePermissionDenied);
    }

    // ---- 2. LOAD. Scoped by the repository to the trusted tenant and the caller's authorized company and
    // branch, so an employee outside that scope is simply not found — never a distinguishable refusal.
    // This is where §8's company and branch authorization is applied, by reusing the existing scoping
    // rather than re-deciding it here.
    var employee = await employees.GetByIdAsync(command.EmployeeId, cancellationToken);
    if (employee is null)
    {
      return Result.Failure(EmployeeErrors.NotFound);
    }

    // ---- 3. DOMAIN PRECONDITIONS, before the destination is looked up. A terminated employee is refused
    // here rather than after a round trip, and an unchanged destination is refused rather than answered
    // with a success that did nothing.
    if (employee.Status == EmployeeStatus.Terminated)
    {
      return Result.Failure(EmployeeErrors.InvalidTransition);
    }

    if (command.DestinationDepartmentId == employee.DepartmentId)
    {
      return Result.Failure(EmployeeErrors.DepartmentUnchanged);
    }

    // ---- 4. OPTIMISTIC CONCURRENCY, checked before the destination lookup so a stale request fails fast —
    // and enforced again by the rowversion token at commit, which is the authoritative check. Two
    // concurrent changes holding the same expected version cannot both append history, because the loser's
    // SaveChanges finds no row to update.
    if (!MatchesExpectedVersion(employee.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(EmployeeErrors.ConcurrencyConflict);
    }

    // ---- 5. THE DESTINATION, ANSWERED EXACTLY AS EMPLOYEE CREATION ANSWERS IT.
    //
    // Same company as the EMPLOYEE, which is the trusted execution company — so a department belonging to
    // another company is reported absent, and an inactive one is named.
    var destination = await CreateEmployeeCommandHandler.ValidateDepartmentAsync(
      employees, companyId, command.DestinationDepartmentId, cancellationToken);
    if (destination.IsFailure)
    {
      return destination;
    }

    // ---- 6. THE DOMAIN MOVES THE EMPLOYEE AND RECORDS THAT IT MOVED, in one step. The appended history is
    // produced by the aggregate rather than assembled here, so a department change without a matching
    // record is not expressible.
    var occurredUtc = clock.UtcNow;
    var changed = employee.ChangeDepartment(
      command.DestinationDepartmentId, command.ReasonCode, command.ReasonText,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (changed.IsFailure)
    {
      return Result.Failure(changed.Error);
    }

    await employees.AppendDepartmentAssignmentAsync(changed.Value, cancellationToken);

    // ---- 7. ONE SAVE. The DepartmentId change, the appended record and the advanced RowVersion commit
    // together or not at all.
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
