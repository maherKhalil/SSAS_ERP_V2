using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.TenantUsers;

public sealed class GetTenantUserByIdQueryHandler(
  ITenantUserReadService readService,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<TenantUserDto>> HandleAsync(GetTenantUserByIdQuery query, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<TenantUserDto>(execution.Error);
    }

    var tenantUser = await readService.GetByIdAsync(query.TenantUserId, cancellationToken);
    return tenantUser is null ? Result.Failure<TenantUserDto>(IdentityAccessErrors.NotFound) : Result.Success(tenantUser);
  }
}
