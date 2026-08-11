namespace SSAS.Platform.Application.Authentication;

// Result of a platform-plane session issuance (ADR-016 Phase 3C / DEC-TEN-0022). Deliberately non-tenant:
// no TenantId, TenantUserId, or role. Anchored to the identity and the platform-support principal.
public sealed record PlatformSessionCreated(
  long PlatformAuthenticationSessionId,
  long IdentityId,
  long PlatformSupportPrincipalId,
  AuthenticationClientId ClientId,
  SensitiveRefreshToken RefreshToken,
  DateTimeOffset RefreshTokenExpiresUtc,
  IssuedAccessToken AccessToken);
