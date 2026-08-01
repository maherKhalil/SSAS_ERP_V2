using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IAuthenticationAccountRepository
{
  Task<AuthenticationAccount?> GetByIdAsync(long authenticationAccountId, CancellationToken cancellationToken = default);

  Task<AuthenticationAccount?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default);

  Task<AuthenticationAccount?> GetByIdForUpdateAsync(long authenticationAccountId, CancellationToken cancellationToken = default);

  Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default);

  Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(
    string normalizedLoginEmail,
    CancellationToken cancellationToken = default);

  Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default);

  Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default);
}
