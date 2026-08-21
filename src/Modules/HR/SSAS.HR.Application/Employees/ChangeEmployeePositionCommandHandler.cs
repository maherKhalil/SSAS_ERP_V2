using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// CHANGE AN EMPLOYEE'S POSITION (FR-POS-0211, DEC-POS-0010, BRULE-POS-0017).
//
// THE COMMAND CARRIES A DESTINATION, NOT A SOURCE — the same rule the branch transfer and the department
// change follow. The source is the employee's current position, read from the record; accepting one from
// the caller would let a request assert where a record used to be.
public sealed record ChangeEmployeePositionCommand(
  Guid EmployeeId,
  Guid DestinationPositionId,
  byte[] ExpectedRowVersion,
  string? ReasonCode = null,
  string? ReasonText = null);

// ================================================================================================
// WHY THIS IS AN UPDATE, ON THE EMPLOYEE PREFIX, AND NOT A PERMISSION OF ITS OWN (DEC-POS-0019)
// ================================================================================================
//
// A branch transfer moves a record across a SECURITY PARTITION: the destination branch decides who may
// subsequently see the employee, which is why it holds its own permission and a dedicated write channel.
//
// A position change moves nothing across any partition. The employee stays in the same tenant, the same
// company and the same branch; only their classification changes. `PositionId` is not a security boundary —
// `DEC-POS-0020` made position a filterable attribute rather than a fourth authorization dimension — so this
// takes `HR.Employees.Update` and opens no channel, exactly as the department change does.
//
// It is emphatically NOT `HR.Positions.Update`: that authorizes editing the job catalog, and giving this
// operation to it would let someone who may rename a position reassign the people holding it.
//
// ---- THE PROMOTION QUESTION WAS ASKED AND DECLINED FOR V1.
//
// A position change is frequently a promotion, and many organizations gate promotions more tightly than an
// ordinary profile edit. `DEC-POS-0019` records that the owner saw that question, weighed a fifth employee
// permission — `HR.Employees.ChangePosition` — and chose not to introduce one. The cost is named and
// accepted: splitting `HR.Employees.Update` later requires re-granting every role that holds it.
//
// ---- BRANCH AND DEPARTMENT ARE ABSENT FROM THIS FILE, AND THAT IS THE ENFORCEMENT (BRULE-POS-0019).
//
// There is no BranchId assignment and no DepartmentId assignment to guard, because a position says nothing
// about either. The three dimensions move independently, and here that is structural rather than asserted.
public sealed class ChangeEmployeePositionCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentCompany currentCompany,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ChangeEmployeePositionCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is null ||
      currentCompany.CompanyId is not { } companyId ||
      string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    // ---- 1. FUNCTIONAL AUTHORITY, BEFORE ANYTHING IS LOADED OR REVEALED.
    if (!currentUser.Permissions.Contains(HrPermissionNames.UpdateEmployees, StringComparer.Ordinal))
    {
      return Result.Failure(EmployeeErrors.WritePermissionDenied);
    }

    // ---- 2. LOAD. Scoped by the repository to the trusted tenant and the caller's authorized company and
    // branch, so an employee outside that scope is simply not found — never a distinguishable refusal.
    var employee = await employees.GetByIdAsync(command.EmployeeId, cancellationToken);
    if (employee is null)
    {
      return Result.Failure(EmployeeErrors.NotFound);
    }

    // ---- 3. DOMAIN PRECONDITIONS, before the destination is looked up. A terminated employee is refused
    // here rather than after a round trip, and an unchanged destination is refused rather than answered
    // with a success that did nothing.
    //
    // A TERMINATED EMPLOYEE KEEPS THEIR POSITION AND CANNOT BE MOVED (`BRULE-POS-0020`). Termination does
    // not clear the position — a historical employment record without a job is unreadable — but a closed
    // record's history must stop moving, which is the same answer `ChangeDepartment` and `Transfer` give.
    if (employee.Status == EmployeeStatus.Terminated)
    {
      return Result.Failure(EmployeeErrors.InvalidTransition);
    }

    if (command.DestinationPositionId == employee.PositionId)
    {
      return Result.Failure(EmployeeErrors.PositionUnchanged);
    }

    // ---- 4. OPTIMISTIC CONCURRENCY, checked before the destination lookup so a stale request fails fast —
    // and enforced again by the rowversion token at commit, which is the authoritative check.
    //
    // This is where `DEC-POS-0021`'s cardinality race is lost by the loser: two concurrent position changes
    // holding the same expected version cannot both append history, because the loser's SaveChanges finds
    // no row to update. The assignment record carries no RowVersion of its own precisely because it is
    // never updated — concurrent changes serialize on the EMPLOYEE's.
    if (!MatchesExpectedVersion(employee.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(EmployeeErrors.ConcurrencyConflict);
    }

    // ---- 5. THE DESTINATION, ANSWERED EXACTLY AS EMPLOYEE CREATION ANSWERS IT.
    //
    // Same company as the EMPLOYEE, which is the trusted execution company — so a position belonging to
    // another company is reported absent, and an inactive one is named (`BRULE-POS-0013`).
    //
    // VALIDATED INSIDE THIS TRANSACTION, which is what `DEC-POS-0021` requires. A position deactivated by a
    // concurrent operation is either seen as inactive here, or was still active when this transaction read
    // it — in which case the deactivation observed an active dependent and is refused on its own side.
    var destination = await CreateEmployeeCommandHandler.ValidatePositionAsync(
      employees, companyId, command.DestinationPositionId, cancellationToken);
    if (destination.IsFailure)
    {
      return destination;
    }

    // ---- 6. THE DOMAIN MOVES THE EMPLOYEE AND RECORDS THAT IT MOVED, in one step. The appended history is
    // produced by the aggregate rather than assembled here, so a position change without a matching record
    // is not expressible (`BRULE-POS-0018`).
    var occurredUtc = clock.UtcNow;
    var changed = employee.ChangePosition(
      command.DestinationPositionId, command.ReasonCode, command.ReasonText,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (changed.IsFailure)
    {
      return Result.Failure(changed.Error);
    }

    await employees.AppendPositionAssignmentAsync(changed.Value, cancellationToken);

    // ---- 7. ONE SAVE. The PositionId change, the appended record and the advanced RowVersion commit
    // together or not at all.
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
