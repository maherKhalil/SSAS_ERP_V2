using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Roles;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface IRoleReadService
{
  Task<RoleDto?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default);

  Task<PagedResult<RoleDto>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
