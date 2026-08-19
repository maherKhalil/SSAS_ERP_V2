using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantUsers;

// REPLACING A TENANT USER'S BRANCH SET (Branch foundation B1b).
//
// Everything decided here is decided under the SAME per-tenant topology lease branch deactivation takes,
// and the current set is re-read from the database rather than accepted from the caller. Both matter: a
// client's idea of the current assignments is a screen that may be minutes old, and the set of ACTIVE
// branches can change under a decision that was correct when it was made (B1a R1).
public sealed class SetTenantUserBranchesCommandHandler(
  ITenantUserRepository tenantUserRepository,
  IUserBranchAccessRepository branchAccessRepository,
  ITenantAdministratorAuthority administratorAuthority,
  ITenantBranchValidator branchValidator,
  IBranchTopologyGuard topologyGuard,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    SetTenantUserBranchesCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return execution;
    }

    var tenantId = execution.Value.TenantId;

    await using var lease = await topologyGuard.AcquireAsync(tenantId, cancellationToken);
    if (lease is null)
    {
      return Result.Failure(BranchErrors.TopologyBusy);
    }

    // ---- AUTHORITATIVE TARGET, read under the lease.
    var tenantUser = await tenantUserRepository.GetByIdAsync(command.TenantUserId, cancellationToken);
    if (tenantUser is null || tenantUser.TenantId != tenantId)
    {
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    var requested = (command.BranchIds ?? []).Distinct().ToArray();
    if (requested.Length != (command.BranchIds?.Count ?? 0))
    {
      return Result.Failure(BranchErrors.AssignmentInvalid);
    }

    // ---- AN ADMINISTRATOR'S SCOPE IS NOT STORED, so there is nothing here to edit. Refused rather than
    // silently ignored: writing rows that do not affect their authority would make the administration
    // screen claim a scope the resolver does not use.
    if (await administratorAuthority.IsTenantAdministratorAsync(
      tenantId, tenantUser.Id, cancellationToken))
    {
      return Result.Failure(BranchErrors.AssignmentInvalid);
    }

    // ---- THE NEVER-ZERO RULE, applied to ACTIVE users.
    //
    // A deactivated membership may sit with whatever assignments history left it; it cannot act. The rule
    // binds when the user is active, and any path that reactivates one must re-check it — see the
    // architecture guard, which names reactivation as the place this would otherwise be bypassed.
    if (tenantUser.Status == TenantUserStatus.Active && requested.Length == 0)
    {
      return Result.Failure(BranchErrors.UserMustHaveAtLeastOneBranch);
    }

    if (requested.Length > 0)
    {
      var assignable = await branchValidator.ValidateAssignableAsync(tenantId, requested, cancellationToken);
      if (assignable.IsFailure)
      {
        return assignable;
      }
    }

    var current = await branchAccessRepository.GetBranchIdsAsync(tenantId, tenantUser.Id, cancellationToken);

    // ONLY THE DIFFERENCE IS TOUCHED, so an unchanged assignment keeps its original CreatedUtc/CreatedBy —
    // deleting and re-inserting the whole set would rewrite when access was granted every time anyone
    // edited the list.
    var toAdd = requested.Except(current).ToArray();
    var toRemove = current.Except(requested).ToArray();

    if (toAdd.Length == 0 && toRemove.Length == 0)
    {
      return Result.Success();
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    await branchAccessRepository.RemoveAsync(tenantId, tenantUser.Id, toRemove, cancellationToken);

    foreach (var branchId in toAdd)
    {
      var access = UserBranchAccess.Create(tenantId, tenantUser.Id, branchId);
      if (access.IsFailure)
      {
        return access;
      }

      await branchAccessRepository.AddAsync(access.Value, cancellationToken);
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return saved;
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success();
  }
}
