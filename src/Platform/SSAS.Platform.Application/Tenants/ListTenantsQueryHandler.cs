using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;

namespace SSAS.Platform.Application.Tenants;

public sealed class ListTenantsQueryHandler(ITenantReadService readService, ICurrentUser currentUser)
{
  public async Task<Result<PagedResult<TenantDto>>> HandleAsync(
    ListTenantsQuery query,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<PagedResult<TenantDto>>(actor.Error);
    }

    if (query.PageNumber < 1 || query.PageSize is < 1 or > 100 ||
      (query.Status.HasValue && !Enum.IsDefined(query.Status.Value)))
    {
      return Result.Failure<PagedResult<TenantDto>>(
        new Error("Tenant.ListFilterInvalid", "Tenant status and paging values must be valid and bounded."));
    }

    return Result.Success(await readService.ListAsync(
      query.Status,
      query.PageNumber,
      query.PageSize,
      cancellationToken));
  }
}
