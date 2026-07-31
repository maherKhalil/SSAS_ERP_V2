using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;

namespace SSAS.Platform.Application.Roles;

public sealed class ListRolesQueryHandler(
  IRoleReadService readService,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<PagedResult<RoleDto>>> HandleAsync(ListRolesQuery query, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<PagedResult<RoleDto>>(execution.Error);
    }

    if (query.PageNumber < 1 || query.PageSize is < 1 or > 100)
    {
      return Result.Failure<PagedResult<RoleDto>>(new Error("Pagination.Invalid", "Page number must be positive and page size must be between 1 and 100."));
    }

    return Result.Success(await readService.ListAsync(query.PageNumber, query.PageSize, cancellationToken));
  }
}
