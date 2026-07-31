using SSAS.Platform.Domain.Identities;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IIdentityRepository
{
  Task<Identity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default);

  Task<Identity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default);

  Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default);

  Task AddAsync(Identity identity, CancellationToken cancellationToken = default);
}
