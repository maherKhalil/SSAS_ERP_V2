using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.PlatformSupport;

public sealed class RevokePlatformPermissionCommandHandler(
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(RevokePlatformPermissionCommand command, CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure(actor.Error);
    }

    var permissionName = PermissionName.Create(command.PermissionName);
    if (permissionName.IsFailure)
    {
      return Result.Failure(PlatformSupportErrors.UnknownPermission);
    }

    var principal = await principalRepository.GetByIdAsync(command.PlatformSupportPrincipalId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure(PlatformSupportErrors.PrincipalNotFound);
    }

    var domainResult = principal.RevokePermission(permissionName.Value, actor.Value, clock.UtcNow);
    return domainResult.IsFailure ? domainResult : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
