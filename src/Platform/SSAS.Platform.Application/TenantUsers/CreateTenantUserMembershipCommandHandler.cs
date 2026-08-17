using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.TenantUsers;

// CREATING A TENANT USER, INCLUDING THE BRANCHES THEY MAY WORK IN (Branch foundation B1b).
//
// ---- ONE PLATFORM TRANSACTION, AND NO DISTRIBUTED TRANSACTION.
//
// The membership, the role assignments and the branch assignments all commit together or not at all: a user
// who exists but can enter nowhere is unusable, and one whose branches landed without their roles is worse.
// The tenant database participates only as a READ — it is asked whether the requested branches are
// assignable — so there is nothing to enlist across catalogs.
//
// ---- THE TOPOLOGY LEASE IS TAKEN FIRST, AND HELD ACROSS VALIDATION AND COMMIT.
//
// This is the other half of B1a's guard. Branch deactivation validates that no user would be stranded and
// then commits; this validates that these branches are active and then commits. Both facts can be
// invalidated by the other operation, so both take the same per-tenant resource. Validating outside the
// lease and trusting the answer afterwards is exactly the race (B1a R2) that leaves a brand-new user
// assigned only to a branch that has just been deactivated.
public sealed class CreateTenantUserMembershipCommandHandler(
  IIdentityRepository identityRepository,
  ITenantUserRepository tenantUserRepository,
  IRoleRepository roleRepository,
  IUserBranchAccessRepository branchAccessRepository,
  ITenantAdministratorAuthority administratorAuthority,
  ITenantBranchValidator branchValidator,
  IBranchTopologyGuard topologyGuard,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<long>> HandleAsync(
    CreateTenantUserMembershipCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<long>(execution.Error);
    }

    var email = EmailAddress.Create(command.Email);
    var displayName = UserDisplayName.Create(command.DisplayName);
    if (email.IsFailure || displayName.IsFailure)
    {
      return Result.Failure<long>(email.IsFailure ? email.Error : displayName.Error);
    }

    var (tenantId, actor) = execution.Value;

    // ---- OWNERSHIP BEFORE ANY DECISION IS MADE.
    await using var lease = await topologyGuard.AcquireAsync(tenantId, cancellationToken);
    if (lease is null)
    {
      return Result.Failure<long>(BranchErrors.TopologyBusy);
    }

    if (await identityRepository.GetByIdAsync(command.IdentityId, cancellationToken) is null)
    {
      return Result.Failure<long>(IdentityAccessErrors.NotFound);
    }

    if (await tenantUserRepository.MembershipExistsAsync(command.IdentityId, cancellationToken))
    {
      return Result.Failure<long>(new Error("TenantUser.MembershipExists", "The identity already has a membership in this tenant."));
    }

    if (await tenantUserRepository.EmailExistsAsync(email.Value.NormalizedEmail, cancellationToken: cancellationToken))
    {
      return Result.Failure<long>(new Error("TenantUser.EmailExists", "The email already exists in this tenant."));
    }

    var roleIds = command.RoleIds?.Distinct().ToArray() ?? [];
    var roles = roleIds.Length == 0
      ? []
      : await roleRepository.GetByIdsAsync(roleIds, cancellationToken);
    if (roles.Count != roleIds.Length)
    {
      return Result.Failure<long>(IdentityAccessErrors.NotFound);
    }

    // ---- WHICH RULE APPLIES. An administrator reaches every active branch by authority, so requiring
    // assignment rows would make the first administrator of an empty tenant impossible to create.
    var willAdminister = await administratorAuthority.RolesConferAdministrationAsync(
      tenantId, roleIds, cancellationToken);

    var branchIds = command.BranchIds ?? [];

    if (willAdminister)
    {
      // Rows would be redundant AND misleading: they would suggest the scope is what they list, when it is
      // actually every active branch and changes as branches are created.
      if (branchIds.Count > 0)
      {
        return Result.Failure<long>(BranchErrors.AssignmentInvalid);
      }
    }
    else
    {
      if (branchIds.Count == 0)
      {
        return Result.Failure<long>(BranchErrors.UserMustHaveAtLeastOneBranch);
      }

      // Validated under the lease, against the tenant database, immediately before the write.
      var assignable = await branchValidator.ValidateAssignableAsync(tenantId, branchIds, cancellationToken);
      if (assignable.IsFailure)
      {
        return Result.Failure<long>(assignable.Error);
      }
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var tenantUser = TenantUser.CreateActive(
      command.IdentityId,
      tenantId,
      email.Value,
      displayName.Value,
      Guid.NewGuid(),
      clock.UtcNow);
    await tenantUserRepository.AddAsync(tenantUser, cancellationToken);

    // The membership identity is needed before the assignment rows can name it.
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<long>(saved.Error);
    }

    foreach (var role in roles)
    {
      var assigned = tenantUser.AssignRole(role, actor, Guid.NewGuid(), clock.UtcNow);
      if (assigned.IsFailure)
      {
        // No commit has happened; disposing the transaction rolls back the membership with it.
        return Result.Failure<long>(assigned.Error);
      }
    }

    foreach (var branchId in branchIds.Distinct())
    {
      var access = UserBranchAccess.Create(tenantId, tenantUser.Id, branchId);
      if (access.IsFailure)
      {
        return Result.Failure<long>(access.Error);
      }

      await branchAccessRepository.AddAsync(access.Value, cancellationToken);
    }

    var committed = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (committed.IsFailure)
    {
      return Result.Failure<long>(committed.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(tenantUser.Id);
  }
}
