using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ICompanyRepository
{
  Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default);

  Task<bool> NormalizedCodeExistsAsync(string normalizedCompanyCode, CancellationToken cancellationToken = default);

  Task AddAsync(Company company, CancellationToken cancellationToken = default);
}
