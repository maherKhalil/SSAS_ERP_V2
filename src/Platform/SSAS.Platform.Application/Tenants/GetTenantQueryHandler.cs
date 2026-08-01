using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Tenants;

public sealed class GetTenantQueryHandler(ITenantReadService readService, ICurrentUser currentUser)
{
  public async Task<Result<TenantDto>> HandleAsync(GetTenantQuery query, CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<TenantDto>(actor.Error);
    }

    var tenant = await readService.GetByIdAsync(query.TenantId, cancellationToken);
    return tenant is null
      ? Result.Failure<TenantDto>(TenantLifecycleErrors.NotFound)
      : Result.Success(tenant);
  }
}
