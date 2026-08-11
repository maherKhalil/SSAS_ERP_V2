using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.Authentication;

// Trusted platform-plane session issuance (ADR-016 Phase 3C / DEC-TEN-0022). Consumes a VerifiedIdentity
// (trusted post-authentication) — never a caller-supplied IdentityId/PrincipalId/permission list. The whole
// operation runs in one UnitOfWork transaction so the session-limit read lock, the session insert, and the
// initial refresh-token insert are atomic and serialized. Live persistence decides eligibility; the token
// permission claims come only from the 3C-1 IPlatformAccessTokenClaimsProvider.
public sealed class PlatformAuthenticationSessionCreator(
  IPlatformAuthenticationSessionRepository sessionRepository,
  IAuthenticationAccountRepository accountRepository,
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformSupportPermissionReadService permissionReadService,
  IPlatformAccessTokenClaimsProvider claimsProvider,
  IAccessTokenIssuer accessTokenIssuer,
  IAuthenticationTokenService tokenService,
  IPlatformUnitOfWork unitOfWork,
  AuthenticationPolicy policy)
{
  public async Task<Result<PlatformSessionCreated>> CreateAsync(
    VerifiedIdentity verifiedIdentity,
    AuthenticationClientId clientId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(verifiedIdentity);
    ArgumentNullException.ThrowIfNull(clientId);
    var now = utcNow.ToUniversalTime();

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    // Live eligibility (persistence, never token/config): account eligible, principal Active, >=1 permission.
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(verifiedIdentity.IdentityId, cancellationToken);
    if (account is not { IsAuthenticationEligible: true })
    {
      return Result.Failure<PlatformSessionCreated>(PlatformSupportErrors.AccountIneligible);
    }

    var principal = await principalRepository.GetByIdentityIdAsync(verifiedIdentity.IdentityId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure<PlatformSessionCreated>(PlatformSupportErrors.PrincipalNotFound);
    }

    if (principal.Status != PlatformSupportPrincipalStatus.Active)
    {
      return Result.Failure<PlatformSessionCreated>(PlatformSupportErrors.PrincipalDisabled);
    }

    var permissions = await permissionReadService.GetActivePermissionsAsync(principal.Id, cancellationToken);
    if (permissions.Count == 0)
    {
      return Result.Failure<PlatformSessionCreated>(PlatformSupportErrors.NoUsablePlatformAuthority);
    }

    // Enforce the platform-only active-session limit under an update lock (platform sessions only).
    var activeSessions = await sessionRepository.ListActiveUnexpiredByIdentityForUpdateAsync(
      verifiedIdentity.IdentityId, now, cancellationToken);
    var revokeCount = Math.Max(0, activeSessions.Count - policy.MaximumActiveSessions + 1);
    foreach (var oldSession in activeSessions
      .OrderBy(session => session.CreatedUtc)
      .ThenBy(session => session.Id)
      .Take(revokeCount))
    {
      var revoke = oldSession.Revoke(PlatformAuthenticationSessionRevocationReason.SessionLimitExceeded, null, now);
      if (revoke.IsFailure)
      {
        return Result.Failure<PlatformSessionCreated>(revoke.Error);
      }
    }

    var absoluteExpiresUtc = now.Add(policy.SessionAbsoluteLifetime);
    var idleExpiresUtc = Min(now.Add(policy.SessionIdleLifetime), absoluteExpiresUtc);
    var session = PlatformAuthenticationSession.Create(
      verifiedIdentity.IdentityId,
      principal.Id,
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
      return Result.Failure<PlatformSessionCreated>(sessionSave.Error);
    }

    var generated = tokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, clientId);
    var refreshToken = session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, now);
    var tokenSave = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (tokenSave.IsFailure)
    {
      return Result.Failure<PlatformSessionCreated>(tokenSave.Error);
    }

    // Claims (and the fresh permission set) come only from the 3C-1 provider; never hand-built here.
    var claims = await claimsProvider.GetClaimsAsync(verifiedIdentity, session.Id, clientId, cancellationToken);
    if (claims.IsFailure)
    {
      return Result.Failure<PlatformSessionCreated>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
    }

    var accessToken = accessTokenIssuer.Issue(claims.Value, now);
    if (accessToken.IsFailure)
    {
      return Result.Failure<PlatformSessionCreated>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
    }

    // Only commit — and only then hand back token material — once persistence has fully succeeded.
    await transaction.CommitAsync(cancellationToken);
    return Result.Success(new PlatformSessionCreated(
      session.Id,
      session.IdentityId,
      session.PlatformSupportPrincipalId,
      clientId,
      generated.SensitiveToken,
      refreshToken.ExpiresUtc,
      accessToken.Value));
  }

  private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
