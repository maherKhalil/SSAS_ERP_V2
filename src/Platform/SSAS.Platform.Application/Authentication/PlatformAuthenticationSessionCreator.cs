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

    // Live eligibility (persistence, never token/config).
    //
    // GLOBAL LOCK ORDER (DEC-TEN-0023 / L1): account → principal → session(s).
    // Every flow that takes BOTH a principal and a session lock takes the principal FIRST — creation here and the
    // Disable handler (which locks the principal for update inside its transaction before listing its sessions).
    // Because the principal row is the single first-contended resource, no cycle can form: a flow can never hold
    // a session lock while waiting for a principal lock. Refresh takes account → session → a NON-locking principal
    // read, so it never waits on the principal lock and cannot close a cycle either.
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(verifiedIdentity.IdentityId, cancellationToken);
    if (account is not { IsAuthenticationEligible: true })
    {
      return Result.Failure<PlatformSessionCreated>(PlatformSupportErrors.AccountIneligible);
    }

    // L1: lock the principal row for update BEFORE acquiring any session resource, then decide Active on that
    // locked read. A concurrent Disable serializes on this same lock, so it cannot commit between this decision
    // and the session insert — no active platform session survives a committed Disable, for either interleaving
    // and independent of the database isolation level (RCSI on or off).
    var principal = await principalRepository.GetByIdentityIdForUpdateAsync(verifiedIdentity.IdentityId, cancellationToken);
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

    // Enforce the platform-only active-session limit under an update lock (platform sessions only), taken AFTER
    // the principal lock so the global account → principal → session order holds.
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
