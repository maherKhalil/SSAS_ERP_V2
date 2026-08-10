using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.PlatformSupport;

public sealed class DisablePlatformSupportPrincipalCommandHandler(
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DisablePlatformSupportPrincipalCommand command,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure(actor.Error);
    }

    var principal = await principalRepository.GetByIdAsync(command.PlatformSupportPrincipalId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure(PlatformSupportErrors.PrincipalNotFound);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(principal.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var domainResult = principal.Disable(actor.Value, clock.UtcNow);
    return domainResult.IsFailure ? domainResult : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
