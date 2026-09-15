using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.TenantUsers;

public sealed record UnlinkEmployeeFromTenantUserCommand(long TenantUserId);

// ==================================================================================================
// REMOVING A LINK (T-092) — AND IT SHIPS WITH THE LINK ROUTE, NOT AFTER IT.
// ==================================================================================================
//
// ---- A LINK CANNOT BE CORRECTED WITHOUT THIS.
//
// `ADR-030` Decision 3 allows at most one live link each way, enforced by two unique indexes, and removal
// is physical with no soft delete. **So a mistaken link occupies both slots**: creating the correct one
// then collides, and the collision is a refusal rather than a repair. Without a removal route the FIRST
// mistake is permanent.
//
// The alternative was an upsert on the link route. **It hides a destructive act inside a creative one** —
// reassigning which employee a login maps to would appear in an audit trail as "create a link" — and given
// the link decides whose payslips a login can read, that act has to be nameable.
//
// ---- IT TAKES ONLY THE TENANT USER.
//
// One live link each way means the user identifies the link uniquely. Taking the employee too would let a
// caller pass a mismatched pair, and the only sensible answer to that is a refusal describing a state the
// caller could have read — so the parameter would exist to be validated rather than used.
//
// ---- REMOVING A LINK DOES NOT TOUCH THE EMPLOYEE OR THE USER.
//
// Neither aggregate knows about the link (`ADR-030` decision 2, no column on either side), so there is
// nothing to cascade and nothing to un-set. **A terminated employee's payslips stop being attributable the
// moment this runs**, which is precisely why `REQ-SS-0006` forbids termination from doing it and why this
// is an administrative correction rather than a lifecycle step.
public sealed class UnlinkEmployeeFromTenantUserCommandHandler(
  IUserEmployeeLinkRepository links,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    UnlinkEmployeeFromTenantUserCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return execution;
    }

    var link = await links.GetByTenantUserAsync(
      execution.Value.TenantId, command.TenantUserId, cancellationToken);

    // ---- NOT IDEMPOTENT, DELIBERATELY, AND THIS IS THE ONE PLACE THE TWO ROUTES DIFFER IN SHAPE.
    //
    // Linking answers success for the identical pair because a retry should not refuse work already done.
    // **Removal refuses when there is nothing to remove**, because "no link" and "I just removed the link"
    // are the same wire answer only if the caller never needed to know which happened — and an
    // administrator repairing a mapping does need to know. A silent success here would let a typo in the
    // tenant user id look like a completed correction.
    if (link is null)
    {
      return Result.Failure(IdentityAccessErrors.LinkNotFound);
    }

    links.Remove(link);

    return await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
