using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class RequestTenantEligibility(
  ITenantAuthenticationEligibilityReadService readService) : IRequestTenantEligibility
{
  private readonly Dictionary<Guid, Task<TenantAuthenticationEligibilityResult>> results = [];

  public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (!results.TryGetValue(tenantId, out var result))
    {
      result = readService.GetEligibilityAsync(tenantId, cancellationToken);
      results.Add(tenantId, result);
    }

    return result;
  }
}
