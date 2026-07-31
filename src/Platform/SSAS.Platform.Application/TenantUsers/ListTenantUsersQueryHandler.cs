using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;

namespace SSAS.Platform.Application.TenantUsers;

public sealed class ListTenantUsersQueryHandler(
  ITenantUserReadService readService,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<PagedResult<TenantUserDto>>> HandleAsync(ListTenantUsersQuery query, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<PagedResult<TenantUserDto>>(execution.Error);
    }

    if (query.PageNumber < 1 || query.PageSize is < 1 or > 100)
    {
      return Result.Failure<PagedResult<TenantUserDto>>(new Error("Pagination.Invalid", "Page number must be positive and page size must be between 1 and 100."));
    }

    return Result.Success(await readService.ListAsync(query.PageNumber, query.PageSize, cancellationToken));
  }
}
