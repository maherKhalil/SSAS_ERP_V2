namespace SSAS.Platform.Application.Authentication;

// Result of a successful platform-plane refresh rotation (ADR-016 Phase 3C / DEC-TEN-0022). Non-tenant.
public sealed record PlatformRefreshSucceeded(
  long PlatformAuthenticationSessionId,
  long IdentityId,
  long PlatformSupportPrincipalId,
  AuthenticationClientId ClientId,
  SensitiveRefreshToken RefreshToken,
  DateTimeOffset RefreshTokenExpiresUtc,
  IssuedAccessToken AccessToken);
