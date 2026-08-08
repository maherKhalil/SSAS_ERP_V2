using SSAS.Platform.Application.Tenants;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface IRequestTenantEligibility
{
  Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);
}
