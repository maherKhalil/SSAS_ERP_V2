using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.TenantUsers;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface ITenantUserReadService
{
  Task<TenantUserDto?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default);

  Task<PagedResult<TenantUserDto>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
