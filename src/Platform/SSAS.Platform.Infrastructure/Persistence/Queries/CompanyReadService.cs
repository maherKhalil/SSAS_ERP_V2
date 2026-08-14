using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// Reads Company from the ROUTED tenant database (ADR-017). Both methods obtain the context through the
// provider, which fails loudly when routing cannot be established — a read path is exactly where a silent
// empty result would be most dangerous, since an unreachable tenant database would otherwise look like an
// empty company list.
public sealed class CompanyReadService(ITenantDbContextProvider contextProvider) : ICompanyReadService
{
  public async Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
  {
    var dbContext = await contextProvider.GetRequiredAsync(cancellationToken);
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
    var dbContext = await contextProvider.GetRequiredAsync(cancellationToken);
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
