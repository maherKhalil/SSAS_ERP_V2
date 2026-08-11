using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

// Live per-identity platform issuance-eligibility + claims preparation (ADR-016 Phase 3C / DEC-TEN-0022).
//
// Fail-closed predicate, evaluated against current persisted state (never a token claim, never bootstrap
// configuration): the authentication account is authentication-eligible AND a PlatformSupportPrincipal
// exists for the identity AND its Status == Active AND it has >= 1 active catalog-valid PlatformSupport
// permission (the read service applies the code-owned catalog filter). Any failure denies issuance.
//
// It composes existing abstractions and creates no session: the trusted AuthenticationSessionId is supplied
// by the caller (Phase 3C-4 session creation; an explicit trusted id in tests). It deliberately does NOT use
// the system-wide IPlatformSupportAuthorityStateReadService, which answers a different question.
public sealed class PlatformAccessTokenClaimsProvider(
  IIdentityRepository identityRepository,
  IAuthenticationAccountRepository accountRepository,
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformSupportPermissionReadService permissionReadService) : IPlatformAccessTokenClaimsProvider
{
  public async Task<Result<PlatformAccessTokenClaims>> GetClaimsAsync(
    VerifiedIdentity verifiedIdentity,
    long authenticationSessionId,
    AuthenticationClientId clientId,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(verifiedIdentity);
    ArgumentNullException.ThrowIfNull(clientId);
    if (authenticationSessionId <= 0)
    {
      return Result.Failure<PlatformAccessTokenClaims>(AuthenticationErrors.InvalidAccessTokenClaims);
    }

    // Account eligibility is the authoritative global check; do not duplicate the formula.
    var account = await accountRepository.GetByIdentityIdAsync(verifiedIdentity.IdentityId, cancellationToken);
    if (account is not { IsAuthenticationEligible: true })
    {
      return Result.Failure<PlatformAccessTokenClaims>(PlatformSupportErrors.AccountIneligible);
    }

    // Live principal status — never trusted from a token; a Disabled principal cannot obtain a token.
    var principal = await principalRepository.GetByIdentityIdAsync(verifiedIdentity.IdentityId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure<PlatformAccessTokenClaims>(PlatformSupportErrors.PrincipalNotFound);
    }

    if (principal.Status != PlatformSupportPrincipalStatus.Active)
    {
      return Result.Failure<PlatformAccessTokenClaims>(PlatformSupportErrors.PrincipalDisabled);
    }

    // Permissions come only from the catalog-filtered platform authority read; zero ⇒ not usable authority.
    var permissions = await permissionReadService.GetActivePermissionsAsync(principal.Id, cancellationToken);
    if (permissions.Count == 0)
    {
      return Result.Failure<PlatformAccessTokenClaims>(PlatformSupportErrors.NoUsablePlatformAuthority);
    }

    var identity = await identityRepository.GetByIdAsync(verifiedIdentity.IdentityId, cancellationToken);
    if (identity is null)
    {
      // Defensive: an eligible account/principal implies the anchoring identity exists.
      return Result.Failure<PlatformAccessTokenClaims>(AuthenticationErrors.InvalidAccessTokenClaims);
    }

    var orderedPermissions = permissions
      .Distinct(StringComparer.Ordinal)
      .OrderBy(value => value, StringComparer.Ordinal)
      .ToArray();

    return Result.Success(new PlatformAccessTokenClaims(
      identity.Subject.Value,
      verifiedIdentity.IdentityId,
      authenticationSessionId,
      clientId,
      account.SecurityVersion,
      orderedPermissions));
  }
}
