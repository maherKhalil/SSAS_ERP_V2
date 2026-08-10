using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.PlatformSupport;

public sealed class RegisterPlatformSupportPrincipalCommandHandler(
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result<long>> HandleAsync(
    RegisterPlatformSupportPrincipalCommand command,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<long>(actor.Error);
    }

    var principal = PlatformSupportPrincipal.Register(command.IdentityId);
    if (principal.IsFailure)
    {
      return Result.Failure<long>(principal.Error);
    }

    if (await principalRepository.ExistsForIdentityAsync(command.IdentityId, cancellationToken))
    {
      return Result.Failure<long>(PlatformSupportErrors.PrincipalAlreadyExists);
    }

    await principalRepository.AddAsync(principal.Value, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure)
    {
      return saveResult.Error == IdentityAccessErrors.UniqueConstraintViolation
        ? Result.Failure<long>(PlatformSupportErrors.PrincipalAlreadyExists)
        : Result.Failure<long>(saveResult.Error);
    }

    return Result.Success(principal.Value.Id);
  }
}
