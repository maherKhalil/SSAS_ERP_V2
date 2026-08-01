using SSAS.Platform.Application.Tenants;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface ITenantAuthenticationEligibilityReadService
{
  Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);

  Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);
}
