using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Platform-plane session/refresh persistence (ADR-016 Phase 3C / DEC-TEN-0022). Every lookup targets ONLY
// the platform tables, so a platform refresh token never resolves against tenant persistence and vice versa
// (structural cross-plane isolation). Update-lock reads mirror the tenant repository's UPDLOCK/HOLDLOCK
// pattern so the future Phase-3C-4 refresh flow can serialize concurrent refresh use.
public sealed class PlatformAuthenticationSessionRepository(PlatformDbContext dbContext)
  : IPlatformAuthenticationSessionRepository
{
  public Task<PlatformRefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(
    Guid refreshTokenPublicId,
    CancellationToken cancellationToken = default) => dbContext.PlatformRefreshTokenRecords
      .AsNoTracking()
      .Where(token => token.PublicId == refreshTokenPublicId)
      .Join(
        dbContext.PlatformAuthenticationSessions.AsNoTracking(),
        token => token.PlatformAuthenticationSessionId,
        session => session.Id,
        (_, session) => new PlatformRefreshTokenSessionLocator(
          session.Id,
          session.IdentityId,
          session.PlatformSupportPrincipalId))
      .SingleOrDefaultAsync(cancellationToken);

  public async Task<PlatformAuthenticationSession?> GetByRefreshTokenForUpdateAsync(
    long platformAuthenticationSessionId,
    CancellationToken cancellationToken = default)
  {
    var session = await LoadSessionForUpdateAsync(platformAuthenticationSessionId, cancellationToken);
    if (session is null)
    {
      return null;
    }

    await LoadTokensForUpdateAsync([session], cancellationToken);
    return session;
  }

  public async Task<PlatformAuthenticationSession?> GetByIdForUpdateAsync(
    long platformAuthenticationSessionId,
    CancellationToken cancellationToken = default)
  {
    var session = await LoadSessionForUpdateAsync(platformAuthenticationSessionId, cancellationToken);
    if (session is null)
    {
      return null;
    }

    await LoadTokensForUpdateAsync([session], cancellationToken);
    return session;
  }

  public async Task<IReadOnlyList<PlatformAuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(
    long identityId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default)
  {
    var sessions = await dbContext.PlatformAuthenticationSessions
      .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformAuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId} AND [Status] = N'Active' AND [IdleExpiresUtc] > {utcNow} AND [AbsoluteExpiresUtc] > {utcNow} ORDER BY [CreatedUtc], [PlatformAuthenticationSessionId]")
      .ToListAsync(cancellationToken);
    await LoadTokensForUpdateAsync(sessions, cancellationToken);
    return sessions;
  }

  public async Task<IReadOnlyList<PlatformAuthenticationSession>> ListActiveByPrincipalForUpdateAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default)
  {
    var sessions = await dbContext.PlatformAuthenticationSessions
      .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformAuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [PlatformSupportPrincipalId] = {platformSupportPrincipalId} AND [Status] = N'Active' ORDER BY [CreatedUtc], [PlatformAuthenticationSessionId]")
      .ToListAsync(cancellationToken);
    await LoadTokensForUpdateAsync(sessions, cancellationToken);
    return sessions;
  }

  public async Task AddAsync(PlatformAuthenticationSession session, CancellationToken cancellationToken = default)
  {
    await dbContext.PlatformAuthenticationSessions.AddAsync(session, cancellationToken);
  }

  private Task<PlatformAuthenticationSession?> LoadSessionForUpdateAsync(
    long platformAuthenticationSessionId,
    CancellationToken cancellationToken) => dbContext.PlatformAuthenticationSessions
      .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformAuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [PlatformAuthenticationSessionId] = {platformAuthenticationSessionId}")
      .SingleOrDefaultAsync(cancellationToken);

  private async Task LoadTokensForUpdateAsync(
    IReadOnlyCollection<PlatformAuthenticationSession> sessions,
    CancellationToken cancellationToken)
  {
    foreach (var session in sessions)
    {
      _ = await dbContext.PlatformRefreshTokenRecords
        .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRefreshTokenRecords] WITH (UPDLOCK, HOLDLOCK) WHERE [PlatformAuthenticationSessionId] = {session.Id}")
        .ToListAsync(cancellationToken);
    }
  }
}
