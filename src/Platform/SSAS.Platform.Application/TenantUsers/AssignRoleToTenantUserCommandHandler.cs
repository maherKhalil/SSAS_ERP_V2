using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.TenantUsers;

public sealed class AssignRoleToTenantUserCommandHandler(
  ITenantUserRepository tenantUserRepository,
  IRoleRepository roleRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(AssignRoleToTenantUserCommand command, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure(execution.Error);
    }

    var tenantUser = await tenantUserRepository.GetByIdAsync(command.TenantUserId, cancellationToken);
    var role = await roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
    if (tenantUser is null || role is null)
    {
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(tenantUser.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var domainResult = tenantUser.AssignRole(role, execution.Value.Actor, Guid.NewGuid(), clock.UtcNow);
    return domainResult.IsFailure ? domainResult : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
