using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.Authentication;

// Platform-plane refresh rotation (ADR-016 Phase 3C / DEC-TEN-0022). Structurally separate from the tenant
// refresh handler: it resolves the refresh token only in platform persistence and never uses a plane selector.
// It re-evaluates live state (account eligibility + SecurityVersion, principal status, catalog-valid platform
// permissions) under an update-lock transaction before rotating, and fails closed (revoke + deny) otherwise.
// Permissions are always re-read live (via the 3C-1 claims provider) — never taken from the old token.
public sealed class RefreshPlatformAuthenticationSessionCommandHandler(
  IAuthenticationAccountRepository accountRepository,
  IPlatformAuthenticationSessionRepository sessionRepository,
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformSupportPermissionReadService permissionReadService,
  IAuthenticationClientRegistry clientRegistry,
  IAuthenticationTokenService tokenService,
  IPlatformAccessTokenClaimsProvider claimsProvider,
  IAccessTokenIssuer accessTokenIssuer,
  IPlatformUnitOfWork unitOfWork,
  AuthenticationPolicy policy,
  IDateTimeProvider clock)
{
  public async Task<Result<PlatformRefreshSucceeded>> HandleAsync(
    RefreshPlatformAuthenticationSessionCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    if (command.RefreshToken is null || command.ClientId is null || !clientRegistry.IsAllowed(command.ClientId) ||
      !tokenService.TryReadPublicId(command.RefreshToken, out var publicId))
    {
      return GenericFailure();
    }

    // Resolve ONLY in the platform store — a tenant refresh token can never be found here.
    var locator = await sessionRepository.GetRefreshTokenLocatorAsync(publicId, cancellationToken);
    if (locator is null)
    {
      return GenericFailure();
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(locator.IdentityId, cancellationToken);
    var session = await sessionRepository.GetByRefreshTokenForUpdateAsync(locator.PlatformAuthenticationSessionId, cancellationToken);
    var token = session?.FindRefreshToken(publicId);
    var now = clock.UtcNow;
    if (session is null || token is null || session.IdentityId != locator.IdentityId ||
      session.PlatformSupportPrincipalId != locator.PlatformSupportPrincipalId ||
      !string.Equals(session.ClientId, command.ClientId.Value, StringComparison.Ordinal) ||
      !tokenService.VerifyRefreshToken(session, token, command.RefreshToken))
    {
      return GenericFailure();
    }

    // Reuse detection: presenting an already-consumed token compromises the session (one-time use).
    if (token.ConsumedUtc.HasValue)
    {
      var compromised = session.MarkCompromised(token, now);
      if (compromised.IsSuccess)
      {
        var compromiseSave = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (compromiseSave.IsFailure)
        {
          return Result.Failure<PlatformRefreshSucceeded>(compromiseSave.Error);
        }

        await transaction.CommitAsync(cancellationToken);
      }

      return GenericFailure();
    }

    if (account is not { IsAuthenticationEligible: true })
    {
      return await RevokeAndFailAsync(session, PlatformAuthenticationSessionRevocationReason.IdentityIneligible, now, transaction, cancellationToken);
    }

    if (account.SecurityVersion != session.SecurityVersionAtCreation)
    {
      return await RevokeAndFailAsync(session, PlatformAuthenticationSessionRevocationReason.SecurityStateChanged, now, transaction, cancellationToken);
    }

    // Live principal status — never trusted from the token; a Disabled/mismatched principal fails closed.
    var principal = await principalRepository.GetByIdentityIdAsync(locator.IdentityId, cancellationToken);
    if (principal is null || principal.Id != session.PlatformSupportPrincipalId ||
      principal.IdentityId != session.IdentityId || principal.Status != PlatformSupportPrincipalStatus.Active)
    {
      return await RevokeAndFailAsync(session, PlatformAuthenticationSessionRevocationReason.PlatformPrincipalIneligible, now, transaction, cancellationToken);
    }

    // Zero usable platform authority ⇒ deny + revoke (DEC-TEN-0019/0022).
    var permissions = await permissionReadService.GetActivePermissionsAsync(principal.Id, cancellationToken);
    if (permissions.Count == 0)
    {
      return await RevokeAndFailAsync(session, PlatformAuthenticationSessionRevocationReason.PlatformPrincipalIneligible, now, transaction, cancellationToken);
    }

    if (!session.IsUsable(now) || !token.IsActive(now))
    {
      return GenericFailure();
    }

    var generated = tokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, command.ClientId);
    var rotated = session.Rotate(token, generated.PublicId, generated.SecretHash, now, policy.SessionIdleLifetime);
    if (rotated.IsFailure)
    {
      return GenericFailure();
    }

    var rotationSave = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (rotationSave.IsFailure)
    {
      return Result.Failure<PlatformRefreshSucceeded>(rotationSave.Error);
    }

    // Fresh permission set: claims come only from the 3C-1 provider, read live — never from the old token.
    var verifiedIdentity = new VerifiedIdentity(account.IdentityId, account.SecurityVersion);
    var claims = await claimsProvider.GetClaimsAsync(verifiedIdentity, session.Id, command.ClientId, cancellationToken);
    if (claims.IsFailure)
    {
      return Result.Failure<PlatformRefreshSucceeded>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
    }

    var accessToken = accessTokenIssuer.Issue(claims.Value, now);
    if (accessToken.IsFailure)
    {
      return Result.Failure<PlatformRefreshSucceeded>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(new PlatformRefreshSucceeded(
      session.Id,
      session.IdentityId,
      session.PlatformSupportPrincipalId,
      command.ClientId,
      generated.SensitiveToken,
      rotated.Value.ExpiresUtc,
      accessToken.Value));
  }

  private async Task<Result<PlatformRefreshSucceeded>> RevokeAndFailAsync(
    PlatformAuthenticationSession session,
    PlatformAuthenticationSessionRevocationReason reason,
    DateTimeOffset utcNow,
    ITransaction transaction,
    CancellationToken cancellationToken)
  {
    if (session.Status == AuthenticationSessionStatus.Active)
    {
      var revoked = session.Revoke(reason, null, utcNow);
      if (revoked.IsFailure)
      {
        return GenericFailure();
      }

      var revokeSave = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (revokeSave.IsFailure)
      {
        return Result.Failure<PlatformRefreshSucceeded>(revokeSave.Error);
      }

      await transaction.CommitAsync(cancellationToken);
    }

    return GenericFailure();
  }

  private static Result<PlatformRefreshSucceeded> GenericFailure() =>
    Result.Failure<PlatformRefreshSucceeded>(AuthenticationErrors.GenericRefreshFailure);
}
