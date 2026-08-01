using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

public sealed class AuthenticationSessionCreator(
  IAuthenticationSessionRepository sessionRepository,
  IPlatformUnitOfWork unitOfWork,
  IAuthenticationTokenService tokenService,
  AuthenticationPolicy policy)
{
  internal async Task<Result<SessionCreated>> CreateAsync(
    AuthenticationAccount account,
    EligibleTenantMembership membership,
    AuthenticationClientId clientId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(membership);
    ArgumentNullException.ThrowIfNull(clientId);
    var now = utcNow.ToUniversalTime();
    if (!account.IsAuthenticationEligible || account.IdentityId != membership.IdentityId)
    {
      return Result.Failure<SessionCreated>(AuthenticationErrors.InvalidAuthenticationSession);
    }

    var activeSessions = await sessionRepository.ListActiveUnexpiredByIdentityForUpdateAsync(
      account.IdentityId,
      now,
      cancellationToken);
    var revokeCount = Math.Max(0, activeSessions.Count - policy.MaximumActiveSessions + 1);
    foreach (var oldSession in activeSessions
      .OrderBy(session => session.CreatedUtc)
      .ThenBy(session => session.Id)
      .Take(revokeCount))
    {
      var revoke = oldSession.Revoke(
        AuthenticationSessionRevocationReason.SessionLimitExceeded,
        null,
        Guid.NewGuid(),
        now);
      if (revoke.IsFailure)
      {
        return Result.Failure<SessionCreated>(revoke.Error);
      }
    }

    var absoluteExpiresUtc = now.Add(policy.SessionAbsoluteLifetime);
    var idleExpiresUtc = Min(now.Add(policy.SessionIdleLifetime), absoluteExpiresUtc);
    var session = AuthenticationSession.Create(
      account.IdentityId,
      membership.TenantUserId,
      membership.TenantId,
      clientId.Value,
      Guid.NewGuid(),
      account.SecurityVersion,
      now,
      idleExpiresUtc,
      absoluteExpiresUtc);
    await sessionRepository.AddAsync(session, cancellationToken);

    var sessionSave = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (sessionSave.IsFailure)
    {
      return Result.Failure<SessionCreated>(sessionSave.Error);
    }

    var generated = tokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, clientId);
    session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, now, Guid.NewGuid());
    var tokenSave = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (tokenSave.IsFailure)
    {
      return Result.Failure<SessionCreated>(tokenSave.Error);
    }

    return Result.Success(new SessionCreated(
      session.Id,
      session.IdentityId,
      session.TenantUserId,
      session.TenantId,
      clientId,
      generated.SensitiveToken));
  }

  private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
