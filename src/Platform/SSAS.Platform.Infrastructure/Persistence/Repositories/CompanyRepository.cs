using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class CompanyRepository(PlatformDbContext dbContext) : ICompanyRepository
{
  public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
    dbContext.Companies.SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);

  // Relies on the automatic ITenantOwnedEntity query filter for tenant scoping; combined with the
  // per-tenant unique index this is an optimization, and the database constraint is authoritative under races.
  public Task<bool> NormalizedCodeExistsAsync(
    string normalizedCompanyCode,
    CancellationToken cancellationToken = default) =>
    dbContext.Companies.AnyAsync(
      company => company.NormalizedCompanyCode == normalizedCompanyCode,
      cancellationToken);

  public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
  {
    await dbContext.Companies.AddAsync(company, cancellationToken);
  }
}
