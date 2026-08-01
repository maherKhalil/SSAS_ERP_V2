using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Tenants;

public sealed class GetTenantAuthenticationEligibilityQueryHandler(
  ITenantAuthenticationEligibilityReadService eligibilityReadService)
{
  public async Task<Result<TenantAuthenticationEligibilityResult>> HandleAsync(
    GetTenantAuthenticationEligibilityQuery query,
    CancellationToken cancellationToken = default) =>
    Result.Success(await eligibilityReadService.GetEligibilityAsync(query.TenantId, cancellationToken));
}
