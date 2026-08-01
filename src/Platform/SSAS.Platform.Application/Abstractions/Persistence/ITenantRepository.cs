using SSAS.Platform.Domain.Tenants;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ITenantRepository
{
  Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

  Task<Tenant?> GetByNormalizedCodeAsync(string normalizedTenantCode, CancellationToken cancellationToken = default);

  Task<bool> NormalizedCodeExistsAsync(string normalizedTenantCode, CancellationToken cancellationToken = default);

  Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
