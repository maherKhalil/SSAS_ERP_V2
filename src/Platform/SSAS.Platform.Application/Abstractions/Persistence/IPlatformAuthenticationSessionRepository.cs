using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Platform-plane session/refresh persistence primitives (ADR-016 Phase 3C / DEC-TEN-0022). All lookups
// resolve ONLY against platform-session persistence, giving structural cross-plane refresh isolation. The
// session-creation and refresh orchestration that consume these primitives are Phase 3C-4; this slice adds
// persistence only.
public interface IPlatformAuthenticationSessionRepository
{
  // Resolve a refresh-token public id to its owning platform session (read-only), or null if not found in
  // the platform store (a tenant refresh token can never be found here).
  Task<PlatformRefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(
    Guid refreshTokenPublicId,
    CancellationToken cancellationToken = default);

  // Load a platform session (and its refresh-token records) under an update lock for refresh rotation.
  Task<PlatformAuthenticationSession?> GetByRefreshTokenForUpdateAsync(
    long platformAuthenticationSessionId,
    CancellationToken cancellationToken = default);

  Task<PlatformAuthenticationSession?> GetByIdForUpdateAsync(
    long platformAuthenticationSessionId,
    CancellationToken cancellationToken = default);

  // Active, unexpired platform sessions for an identity under an update lock — for session-limit enforcement
  // at creation. Platform-only: tenant sessions never participate.
  Task<IReadOnlyList<PlatformAuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(
    long identityId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default);

  // Active platform sessions for a principal under an update lock — for future proactive Disable revocation.
  Task<IReadOnlyList<PlatformAuthenticationSession>> ListActiveByPrincipalForUpdateAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default);

  Task AddAsync(PlatformAuthenticationSession session, CancellationToken cancellationToken = default);
}
