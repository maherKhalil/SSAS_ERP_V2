using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.PlatformSupport;

// Current EFFECTIVE platform-support authority for one principal (DEC-TEN-0025, Phase 4C): the active,
// current-catalog-valid PermissionScope.PlatformSupport permission names. This deliberately reuses the
// existing IPlatformSupportPermissionReadService (the same catalog-filtered semantics used by token issuance)
// so there is one source of truth for "effective authority" — distinct from the raw assignment history.
public sealed class GetActivePlatformSupportPermissionsQueryHandler(
  IPlatformSupportAuthorityReadService authorityReadService,
  IPlatformSupportPermissionReadService permissionReadService,
  ICurrentUser currentUser)
{
  public async Task<Result<IReadOnlyCollection<string>>> HandleAsync(
    GetActivePlatformSupportPermissionsQuery query,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<IReadOnlyCollection<string>>(actor.Error);
    }

    if (!await authorityReadService.PrincipalExistsAsync(query.PlatformSupportPrincipalId, cancellationToken))
    {
      return Result.Failure<IReadOnlyCollection<string>>(PlatformSupportErrors.PrincipalNotFound);
    }

    var permissions = await permissionReadService.GetActivePermissionsAsync(query.PlatformSupportPrincipalId, cancellationToken);
    return Result.Success(permissions);
  }
}
