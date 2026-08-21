using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

public sealed record DeactivatePositionCommand(Guid PositionId, byte[] RowVersion);

public sealed record ReactivatePositionCommand(Guid PositionId, byte[] RowVersion);

// DEACTIVATE A POSITION (FR-POS-0205, BRULE-POS-0014).
//
// ================================================================================================
// IT CONSULTS NO INCUMBENTS, AND UNDER `OD-POS-005` IT MUST NOT.
// ================================================================================================
//
// The owner ruled the ASSIGNMENT reading of `BR-HR-0006`: "one active position" qualifies the assignment,
// not the position's lifecycle status. So the employees holding this position keep it, `BR-HR-0006` remains
// satisfied for every one of them, and the deactivation is a pure state transition.
//
// The alternative reading was considered and rejected: had *active* qualified the position's status,
// deactivating a position with incumbents would have broken `BR-HR-0006` for all of them at that instant and
// would have had to be refused — using one rule to break another, which FP-007 declined to do for
// departments and which this declines for the same reason.
//
// What an inactive position refuses is a NEW arrival (`BRULE-POS-0013`), and that refusal belongs to the
// operation doing the assigning — Employee creation and `ChangePosition`, both Phase 3.
//
// ---- THE ASYMMETRY WITH GRADES IS THE POINT.
//
// A grade with active dependents may NOT be deactivated (`DEC-POS-0013`). An employee's assignment is a fact
// about a person that survives the position's retirement; a grade reference is a structural pointer, and
// deactivating its target would leave an Active position aimed at an Inactive grade.
public sealed class DeactivatePositionCommandHandler(
  IPositionRepository positions,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DeactivatePositionCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    var loaded = await PositionWriteContext.LoadAsync(
      positions, scope, currentTenant, command.PositionId,
      HrPermissionNames.DeactivatePositions, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    // Already inactive returns InvalidTransition from the aggregate — the project's established lifecycle
    // convention (Employee and Department both do the same), not a new idempotency rule invented here.
    var deactivated = loaded.Value.Deactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (deactivated.IsFailure)
    {
      return deactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

// REACTIVATE A POSITION (FR-POS-0205).
//
// ---- IT HAS NO PARENT CHAIN TO CHECK, AND THAT IS A CONSEQUENCE OF A RULING RATHER THAN A SIMPLIFICATION.
//
// `ReactivateDepartmentCommandHandler` must refuse an active child beneath an inactive parent, because
// departments form a tree. `OD-POS-006` deferred the position hierarchy, so a Position has no parent, and
// `OD-POS-003` ruled it independent of Department, so it has no owner in another tree either.
//
// ---- BUT THE GRADE REFERENCE IS NOT RE-CHECKED HERE EITHER, AND THAT IS DELIBERATE.
//
// A position may be reactivated while pointing at a grade that has since been deactivated. Refusing would
// strand the position — a caller holding only `HR.Positions.Deactivate` cannot re-point it, because
// re-grading lives under `HR.Positions.Update`, so the refusal would be unactionable for exactly the role
// the permission was split to serve. The reference is re-validated by the operation that can fix it.
public sealed class ReactivatePositionCommandHandler(
  IPositionRepository positions,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ReactivatePositionCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    // THE SAME PERMISSION GUARDS BOTH DIRECTIONS (DEC-DEP-0025). Granting reactivation under ordinary
    // Update would let a caller who may only retitle undo a closure someone deliberately made.
    var loaded = await PositionWriteContext.LoadAsync(
      positions, scope, currentTenant, command.PositionId,
      HrPermissionNames.DeactivatePositions, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var reactivated = loaded.Value.Reactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (reactivated.IsFailure)
    {
      return reactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}
