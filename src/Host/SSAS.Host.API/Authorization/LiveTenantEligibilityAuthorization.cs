using SSAS.Platform.Application.Abstractions.Queries;

namespace SSAS.Host.API.Authorization;

public sealed class LiveTenantEligibilityAuthorization(ITenantAuthenticationEligibilityReadService readService)
{
  private Guid? cachedTenantId;
  private Task<bool>? cachedResult;

  public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken)
  {
    if (cachedResult is null || cachedTenantId != tenantId)
    {
      cachedTenantId = tenantId;
      cachedResult = ReadAsync(tenantId, cancellationToken);
    }
    return cachedResult;
  }

  private async Task<bool> ReadAsync(Guid tenantId, CancellationToken cancellationToken) =>
    (await readService.GetEligibilityAsync(tenantId, cancellationToken)).IsAuthenticationEligible;
}
