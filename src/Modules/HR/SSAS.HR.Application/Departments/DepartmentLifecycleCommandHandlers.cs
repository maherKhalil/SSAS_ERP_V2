using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments;

public sealed record DeactivateDepartmentCommand(Guid DepartmentId, byte[] RowVersion);

public sealed record ReactivateDepartmentCommand(Guid DepartmentId, byte[] RowVersion);

// DEACTIVATE A DEPARTMENT (BR-HR-0009).
//
// ---- IT REFUSES WHILE ACTIVE CHILDREN REMAIN, AND DOES NOT CASCADE.
//
// A cascade would deactivate an arbitrary amount of structure from one click, and it would destroy the
// information needed to reverse it: reactivating could not tell which descendants were already inactive
// beforehand. Refusing until the children are handled is more work for the operator and is the only version
// that is actually reversible.
//
// Inactive children do not block it — they are already in the state the parent is moving to.
//
// ---- IT DOES NOT TOUCH EMPLOYEES, AND CANNOT.
//
// `Employee.DepartmentId` does not exist until Phase 3, so there is nothing to query. Even once it does,
// `BR-HR-0009` refuses NEW arrivals rather than expelling existing members: expelling them would violate
// `BR-HR-0005` for every one of them the instant a department was deactivated, using one rule to break
// another.
//
// ---- IT DOES NOT CLEAR THE MANAGER.
//
// The record of who headed a department is part of what makes the inactive department readable.
public sealed class DeactivateDepartmentCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DeactivateDepartmentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(DepartmentErrors.InvalidActor);
    }

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.DeactivateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var department = loaded.Value;

    if (await departments.HasActiveChildrenAsync(department.Id, cancellationToken))
    {
      return Result.Failure(DepartmentErrors.HasActiveChildren);
    }

    // Already inactive returns InvalidTransition from the aggregate — the project's established lifecycle
    // convention (Employee does the same), not a new idempotency rule invented here.
    var deactivated = department.Deactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (deactivated.IsFailure)
    {
      return deactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

// REACTIVATE A DEPARTMENT.
//
// ---- THE PARENT MUST BE ACTIVE FIRST.
//
// An active child beneath an inactive parent is an incoherent tree, and it is reachable in exactly one way:
// deactivate a leaf, deactivate its parent, then reactivate the leaf. This closes that path.
//
// It is the mirror of the rule `ChangeDepartmentParent` already enforces — a department may not be MOVED
// beneath an inactive parent — so the two together mean an active department always has an active parent
// chain, whichever operation produced it. No cascade in either direction.
public sealed class ReactivateDepartmentCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ReactivateDepartmentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(DepartmentErrors.InvalidActor);
    }

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.DeactivateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var department = loaded.Value;

    if (department.ParentDepartmentId is { } parentId)
    {
      var parent = await departments.GetByIdAsync(parentId, cancellationToken);
      if (parent is null)
      {
        return Result.Failure(DepartmentErrors.ParentNotFound);
      }

      if (parent.Status != DepartmentStatus.Active)
      {
        return Result.Failure(DepartmentErrors.ParentInactive);
      }
    }

    var reactivated = department.Reactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (reactivated.IsFailure)
    {
      return reactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}
