using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class CompanyReadService(PlatformDbContext dbContext) : ICompanyReadService
{
  public async Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
  {
    var company = await dbContext.Companies.AsNoTracking()
      .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
    return company is null ? null : Map(company);
  }

  public async Task<PagedResult<CompanyDto>> ListAsync(
    CompanyStatus? status,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default)
  {
    var query = dbContext.Companies.AsNoTracking();
    if (status.HasValue)
    {
      query = query.Where(company => company.Status == status.Value);
    }

    var totalCount = await query.CountAsync(cancellationToken);
    var companies = await query
      .OrderBy(company => company.CompanyName)
      .ThenBy(company => company.Id)
      .Skip((pageNumber - 1) * pageSize)
      .Take(pageSize)
      .ToArrayAsync(cancellationToken);
    return new PagedResult<CompanyDto>(companies.Select(Map).ToArray(), pageNumber, pageSize, totalCount);
  }

  private static CompanyDto Map(Company company) => new(
    company.CompanyId,
    company.TenantId,
    company.CompanyCode.Value,
    company.CompanyName.Value,
    company.BaseCurrencyCode.Value,
    company.Status,
    company.CreatedUtc,
    company.CreatedBy,
    company.ModifiedUtc,
    company.ModifiedBy,
    company.StatusChangedUtc,
    company.StatusChangedBy,
    company.StatusChangeReasonCode,
    company.RowVersion.ToArray());
}
