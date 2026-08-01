using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface ITenantReadService
{
  Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

  Task<PagedResult<TenantDto>> ListAsync(
    TenantStatus? status,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default);
}
