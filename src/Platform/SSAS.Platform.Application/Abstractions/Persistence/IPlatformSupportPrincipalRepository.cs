using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IPlatformSupportPrincipalRepository
{
  Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default);

  Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default);

  Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default);

  Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default);
}
