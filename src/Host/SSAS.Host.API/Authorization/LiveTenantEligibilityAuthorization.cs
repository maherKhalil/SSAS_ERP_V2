using SSAS.Platform.Application.Abstractions.Queries;

namespace SSAS.Host.API.Authorization;

public sealed class LiveTenantEligibilityAuthorization(IRequestTenantEligibility eligibility)
{
  public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken) =>
    ReadAsync(tenantId, cancellationToken);

  private async Task<bool> ReadAsync(Guid tenantId, CancellationToken cancellationToken) =>
    (await eligibility.GetEligibilityAsync(tenantId, cancellationToken)).IsAuthenticationEligible;
}
