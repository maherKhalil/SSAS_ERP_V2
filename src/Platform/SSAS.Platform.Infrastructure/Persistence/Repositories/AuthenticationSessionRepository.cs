using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class AuthenticationSessionRepository(PlatformDbContext dbContext) : IAuthenticationSessionRepository
{
  public Task<RefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(
    Guid refreshTokenPublicId,
    CancellationToken cancellationToken = default) => dbContext.RefreshTokenRecords
      .AsNoTracking()
      .Where(token => token.PublicId == refreshTokenPublicId)
      .Join(
        dbContext.AuthenticationSessions.AsNoTracking(),
        token => token.AuthenticationSessionId,
        session => session.Id,
        (_, session) => new RefreshTokenSessionLocator(
          session.Id,
          session.IdentityId,
          session.TenantUserId,
          session.TenantId))
      .SingleOrDefaultAsync(cancellationToken);

  public async Task<AuthenticationSession?> GetByRefreshTokenForUpdateAsync(
    long authenticationSessionId,
    CancellationToken cancellationToken = default)
  {
    var session = await dbContext.AuthenticationSessions
      .FromSqlInterpolated($"SELECT * FROM [platform].[AuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [AuthenticationSessionId] = {authenticationSessionId}")
      .SingleOrDefaultAsync(cancellationToken);
    if (session is null)
    {
      return null;
    }

    _ = await dbContext.RefreshTokenRecords
      .FromSqlInterpolated($"SELECT * FROM [platform].[RefreshTokenRecords] WITH (UPDLOCK, HOLDLOCK) WHERE [AuthenticationSessionId] = {authenticationSessionId}")
      .ToListAsync(cancellationToken);
    return session;
  }

  public async Task<IReadOnlyList<AuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(
    long identityId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default)
  {
    var sessions = await dbContext.AuthenticationSessions
      .FromSqlInterpolated($"SELECT * FROM [platform].[AuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId} AND [Status] = N'Active' AND [IdleExpiresUtc] > {utcNow} AND [AbsoluteExpiresUtc] > {utcNow} ORDER BY [CreatedUtc], [AuthenticationSessionId]")
      .ToListAsync(cancellationToken);
    await LoadTokensForUpdateAsync(sessions, cancellationToken);
    return sessions;
  }

  public async Task<IReadOnlyList<AuthenticationSession>> ListActiveByIdentityForUpdateAsync(
    long identityId,
    CancellationToken cancellationToken = default)
  {
    var sessions = await dbContext.AuthenticationSessions
      .FromSqlInterpolated($"SELECT * FROM [platform].[AuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId} AND [Status] = N'Active' ORDER BY [CreatedUtc], [AuthenticationSessionId]")
      .ToListAsync(cancellationToken);
    await LoadTokensForUpdateAsync(sessions, cancellationToken);
    return sessions;
  }

  public async Task AddAsync(AuthenticationSession session, CancellationToken cancellationToken = default)
  {
    await dbContext.AuthenticationSessions.AddAsync(session, cancellationToken);
  }

  private async Task LoadTokensForUpdateAsync(
    IReadOnlyCollection<AuthenticationSession> sessions,
    CancellationToken cancellationToken)
  {
    foreach (var session in sessions)
    {
      _ = await dbContext.RefreshTokenRecords
        .FromSqlInterpolated($"SELECT * FROM [platform].[RefreshTokenRecords] WITH (UPDLOCK, HOLDLOCK) WHERE [AuthenticationSessionId] = {session.Id}")
        .ToListAsync(cancellationToken);
    }
  }
}
