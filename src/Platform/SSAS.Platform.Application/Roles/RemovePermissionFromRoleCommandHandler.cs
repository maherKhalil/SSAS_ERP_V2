using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Roles;

public sealed class RemovePermissionFromRoleCommandHandler(
  IRoleRepository roleRepository,
  IPermissionCatalog permissionCatalog,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(RemovePermissionFromRoleCommand command, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure(execution.Error);
    }

    if (!permissionCatalog.TryGet(command.PermissionName, out var permission))
    {
      return Result.Failure(IdentityAccessErrors.InvalidPermission);
    }

    var role = await roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
    if (role is null)
    {
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(role.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var domainResult = role.RemovePermission(permission.Name, execution.Value.Actor, Guid.NewGuid(), clock.UtcNow);
    return domainResult.IsFailure ? domainResult : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
