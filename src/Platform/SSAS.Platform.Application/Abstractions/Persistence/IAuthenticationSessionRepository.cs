using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IAuthenticationSessionRepository
{
  Task<RefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(Guid refreshTokenPublicId, CancellationToken cancellationToken = default);

  Task<AuthenticationSession?> GetByRefreshTokenForUpdateAsync(long authenticationSessionId, CancellationToken cancellationToken = default);

  Task<AuthenticationSession?> GetByIdForUpdateAsync(long authenticationSessionId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<AuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(
    long identityId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<AuthenticationSession>> ListActiveByIdentityForUpdateAsync(
    long identityId,
    CancellationToken cancellationToken = default);

  Task AddAsync(AuthenticationSession session, CancellationToken cancellationToken = default);
}
