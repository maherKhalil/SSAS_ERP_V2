using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class TenantAuthenticationEligibilityReadService(PlatformDbContext dbContext)
  : ITenantAuthenticationEligibilityReadService
{
  public async Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    var status = await dbContext.Tenants.AsNoTracking()
      .Where(tenant => tenant.Id == tenantId)
      .Select(tenant => (TenantStatus?)tenant.Status)
      .SingleOrDefaultAsync(cancellationToken);
    return TenantAuthenticationEligibilityResult.FromStatus(tenantId, status);
  }
}
