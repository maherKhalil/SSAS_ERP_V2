using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Authentication;

// Prepares platform-plane access-token claims for a trusted, already-authenticated identity
// (ADR-016 Phase 3C / DEC-TEN-0022). It performs the live per-identity issuance-eligibility predicate and
// sources permissions from IPlatformSupportPermissionReadService. It is the platform counterpart of the
// tenant IAccessTokenClaimsProvider and is intentionally separate: the tenant provider stays tenant-only.
//
// It does NOT create a session. The AuthenticationSessionId is supplied by the (future Phase 3C-4) caller
// that owns the persisted PlatformAuthenticationSession; in tests it is an explicitly provided trusted id.
public interface IPlatformAccessTokenClaimsProvider
{
  Task<Result<PlatformAccessTokenClaims>> GetClaimsAsync(
    VerifiedIdentity verifiedIdentity,
    long authenticationSessionId,
    AuthenticationClientId clientId,
    CancellationToken cancellationToken = default);
}
