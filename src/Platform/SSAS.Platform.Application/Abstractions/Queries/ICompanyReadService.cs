using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Abstractions.Queries;

public interface ICompanyReadService
{
  Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default);

  Task<PagedResult<CompanyDto>> ListAsync(
    CompanyStatus? status,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default);
}
