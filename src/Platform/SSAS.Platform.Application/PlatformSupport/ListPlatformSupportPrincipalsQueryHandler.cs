using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;

namespace SSAS.Platform.Application.PlatformSupport;

// Paginated, deterministically-ordered list of platform-support principals (DEC-TEN-0025, Phase 4C).
// Mirrors the existing platform-plane read handlers: GetPlatformActor is Application-layer defense in depth
// (a trusted actor exists) — it is NOT the Phase-4D permission check, which the future HTTP route enforces
// with RequirePlatformPermission(Platform.Support.Administer).
public sealed class ListPlatformSupportPrincipalsQueryHandler(
  IPlatformSupportAuthorityReadService readService,
  ICurrentUser currentUser)
{
  public async Task<Result<PagedResult<PlatformSupportPrincipalDto>>> HandleAsync(
    ListPlatformSupportPrincipalsQuery query,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<PagedResult<PlatformSupportPrincipalDto>>(actor.Error);
    }

    if (query.PageNumber < 1 || query.PageSize is < 1 or > 100)
    {
      return Result.Failure<PagedResult<PlatformSupportPrincipalDto>>(
        new Error("PlatformSupport.ListFilterInvalid", "Paging values must be valid and bounded."));
    }

    return Result.Success(await readService.ListPrincipalsAsync(query.PageNumber, query.PageSize, cancellationToken));
  }
}
