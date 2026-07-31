using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Roles;

public sealed class UpdateCustomRoleCommandHandler(
  IRoleRepository roleRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(UpdateCustomRoleCommand command, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure(execution.Error);
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

    var name = RoleName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure(name.Error);
    }

    if (await roleRepository.NameExistsAsync(name.Value.NormalizedRoleName, role.Id, cancellationToken))
    {
      return Result.Failure(new Error("Role.NameExists", "The role name already exists in this tenant."));
    }

    var domainResult = role.Update(name.Value, command.Description, Guid.NewGuid(), clock.UtcNow);
    return domainResult.IsFailure ? domainResult : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
