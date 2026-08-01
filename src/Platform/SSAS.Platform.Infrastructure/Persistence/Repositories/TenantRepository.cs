using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(PlatformDbContext dbContext) : ITenantRepository
{
  public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
    dbContext.Tenants.SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);

  public Task<Tenant?> GetByNormalizedCodeAsync(
    string normalizedTenantCode,
    CancellationToken cancellationToken = default) =>
    dbContext.Tenants.SingleOrDefaultAsync(
      tenant => tenant.NormalizedTenantCode == normalizedTenantCode,
      cancellationToken);

  public Task<bool> NormalizedCodeExistsAsync(
    string normalizedTenantCode,
    CancellationToken cancellationToken = default) =>
    dbContext.Tenants.AnyAsync(tenant => tenant.NormalizedTenantCode == normalizedTenantCode, cancellationToken);

  public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
  {
    await dbContext.Tenants.AddAsync(tenant, cancellationToken);
  }
}
