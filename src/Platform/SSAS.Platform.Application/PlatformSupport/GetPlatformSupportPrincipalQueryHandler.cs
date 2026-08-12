using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.PlatformSupport;

// Single platform-support principal read for authority administration (DEC-TEN-0025, Phase 4C). A missing
// principal returns the conventional not-found error; Disabled principals remain readable.
public sealed class GetPlatformSupportPrincipalQueryHandler(
  IPlatformSupportAuthorityReadService readService,
  ICurrentUser currentUser)
{
  public async Task<Result<PlatformSupportPrincipalDto>> HandleAsync(
    GetPlatformSupportPrincipalQuery query,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<PlatformSupportPrincipalDto>(actor.Error);
    }

    var principal = await readService.GetPrincipalAsync(query.PlatformSupportPrincipalId, cancellationToken);
    return principal is null
      ? Result.Failure<PlatformSupportPrincipalDto>(PlatformSupportErrors.PrincipalNotFound)
      : Result.Success(principal);
  }
}
