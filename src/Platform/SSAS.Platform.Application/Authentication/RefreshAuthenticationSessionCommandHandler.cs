using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

public sealed class RefreshAuthenticationSessionCommandHandler(
  IAuthenticationAccountRepository accountRepository,
  IAuthenticationSessionRepository sessionRepository,
  IIdentityTenantMembershipReadService membershipReadService,
  IAuthenticationClientRegistry clientRegistry,
  IAuthenticationTokenService tokenService,
  IAccessTokenClaimsProvider claimsProvider,
  IAccessTokenIssuer accessTokenIssuer,
  IPlatformUnitOfWork unitOfWork,
  AuthenticationPolicy policy,
  IDateTimeProvider clock)
{
  public async Task<Result<RefreshSucceeded>> HandleAsync(
    RefreshAuthenticationSessionCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    if (command.RefreshToken is null || command.ClientId is null || !clientRegistry.IsAllowed(command.ClientId) ||
      !tokenService.TryReadPublicId(command.RefreshToken, out var publicId))
    {
      return GenericFailure();
    }

    var locator = await sessionRepository.GetRefreshTokenLocatorAsync(publicId, cancellationToken);
    if (locator is null)
    {
      return GenericFailure();
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(locator.IdentityId, cancellationToken);
    IdentityTenantMembershipEligibility? eligibility = null;
    if (account is { IsAuthenticationEligible: true } && account.SecurityVersion > 0)
    {
      eligibility = await membershipReadService.GetMembershipEligibilityForUpdateAsync(
        locator.IdentityId,
        locator.TenantUserId,
        locator.TenantId,
        cancellationToken);
    }

    var session = await sessionRepository.GetByRefreshTokenForUpdateAsync(locator.AuthenticationSessionId, cancellationToken);
    var token = session?.FindRefreshToken(publicId);
    var now = clock.UtcNow;
    if (session is null || token is null || session.IdentityId != locator.IdentityId ||
      session.TenantUserId != locator.TenantUserId || session.TenantId != locator.TenantId ||
      !string.Equals(session.ClientId, command.ClientId.Value, StringComparison.Ordinal) ||
      !tokenService.VerifyRefreshToken(session, token, command.RefreshToken))
    {
      return GenericFailure();
    }

    if (account is null || !account.IsAuthenticationEligible)
    {
      return await RevokeAndFailAsync(session, AuthenticationSessionRevocationReason.IdentityIneligible, now, transaction, cancellationToken);
    }

    if (account.SecurityVersion != session.SecurityVersionAtCreation)
    {
      return await RevokeAndFailAsync(session, AuthenticationSessionRevocationReason.SecurityStateChanged, now, transaction, cancellationToken);
    }

    if (eligibility?.Membership is null)
    {
      return await RevokeAndFailAsync(session, AuthenticationSessionRevocationReason.MembershipIneligible, now, transaction, cancellationToken);
    }

    if (!eligibility.IsTenantEligible)
    {
      return await RevokeAndFailAsync(session, AuthenticationSessionRevocationReason.TenantIneligible, now, transaction, cancellationToken);
    }

    if (token.ConsumedUtc.HasValue)
    {
      var compromised = session.MarkCompromised(token, Guid.NewGuid(), Guid.NewGuid(), now);
      if (compromised.IsFailure)
      {
        return GenericFailure();
      }

      var compromiseSave = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (compromiseSave.IsFailure) return Result.Failure<RefreshSucceeded>(compromiseSave.Error);

      await transaction.CommitAsync(cancellationToken);
      return GenericFailure();
    }

    if (!session.IsUsable(now) || !token.IsActive(now))
    {
      return GenericFailure();
    }

    var generated = tokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, command.ClientId);
    var rotated = session.Rotate(
      token,
      generated.PublicId,
      generated.SecretHash,
      now,
      policy.SessionIdleLifetime,
      Guid.NewGuid());
    if (rotated.IsFailure)
    {
      return GenericFailure();
    }

    var rotationSave = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (rotationSave.IsFailure) return Result.Failure<RefreshSucceeded>(rotationSave.Error);

    var claims = await claimsProvider.GetClaimsAsync(
      session.Id,
      session.IdentityId,
      session.TenantUserId,
      session.TenantId,
      command.ClientId,
      session.SecurityVersionAtCreation,
      cancellationToken);
    if (claims.IsFailure)
    {
      return Result.Failure<RefreshSucceeded>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
    }

    var accessToken = accessTokenIssuer.Issue(claims.Value, now);
    if (accessToken.IsFailure)
    {
      return Result.Failure<RefreshSucceeded>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(new RefreshSucceeded(
      session.Id,
      session.IdentityId,
      session.TenantUserId,
      session.TenantId,
      command.ClientId,
      generated.SensitiveToken,
      rotated.Value.ExpiresUtc,
      accessToken.Value));
  }

  private async Task<Result<RefreshSucceeded>> RevokeAndFailAsync(
    Domain.Authentication.AuthenticationSession session,
    AuthenticationSessionRevocationReason reason,
    DateTimeOffset utcNow,
    BuildingBlocks.Application.Abstractions.Persistence.ITransaction transaction,
    CancellationToken cancellationToken)
  {
    if (session.Status == AuthenticationSessionStatus.Active)
    {
      var revoked = session.Revoke(reason, null, Guid.NewGuid(), utcNow);
      if (revoked.IsFailure)
      {
        return GenericFailure();
      }

      var revokeSave = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (revokeSave.IsFailure) return Result.Failure<RefreshSucceeded>(revokeSave.Error);

      await transaction.CommitAsync(cancellationToken);
    }

    return GenericFailure();
  }

  private static Result<RefreshSucceeded> GenericFailure() =>
    Result.Failure<RefreshSucceeded>(AuthenticationErrors.GenericRefreshFailure);
}
