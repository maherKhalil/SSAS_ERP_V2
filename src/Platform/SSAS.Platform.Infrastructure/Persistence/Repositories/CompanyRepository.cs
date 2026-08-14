using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Company is tenant ERP data and is therefore read and written through the ROUTED TenantDbContext
// (ADR-017), not PlatformDbContext. The provider resolves routing once per unit of work; if no trusted
// route exists it throws rather than returning empty, so "storage unavailable" is never mistaken for
// "no such company".
public sealed class CompanyRepository(ITenantDbContextProvider contextProvider) : ICompanyRepository
{
  public async Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
  {
    var dbContext = await contextProvider.GetRequiredAsync(cancellationToken);
    return await dbContext.Companies.SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);
  }

  // Relies on the automatic ITenantOwnedEntity query filter for tenant scoping; combined with the
  // per-tenant unique index this is an optimization, and the database constraint is authoritative under races.
  public async Task<bool> NormalizedCodeExistsAsync(
    string normalizedCompanyCode,
    CancellationToken cancellationToken = default)
  {
    var dbContext = await contextProvider.GetRequiredAsync(cancellationToken);
    return await dbContext.Companies.AnyAsync(
      company => company.NormalizedCompanyCode == normalizedCompanyCode,
      cancellationToken);
  }

  public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
  {
    var dbContext = await contextProvider.GetRequiredAsync(cancellationToken);
    await dbContext.Companies.AddAsync(company, cancellationToken);
  }
}
