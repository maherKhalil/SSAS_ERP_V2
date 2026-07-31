using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Roles;

public sealed class CreateCustomRoleCommandHandler(
  IRoleRepository roleRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<long>> HandleAsync(CreateCustomRoleCommand command, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<long>(execution.Error);
    }

    var name = RoleName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure<long>(name.Error);
    }

    if (await roleRepository.NameExistsAsync(name.Value.NormalizedRoleName, cancellationToken: cancellationToken))
    {
      return Result.Failure<long>(new Error("Role.NameExists", "The role name already exists in this tenant."));
    }

    var role = Role.CreateCustom(execution.Value.TenantId, name.Value, command.Description, Guid.NewGuid(), clock.UtcNow);
    await roleRepository.AddAsync(role, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saveResult.IsFailure ? Result.Failure<long>(saveResult.Error) : Result.Success(role.Id);
  }
}
