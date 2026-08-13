namespace SSAS.Platform.API.Authentication;

// Platform-support authentication success response (Phase 4B / DEC-TEN-0023). Non-tenant: it carries the
// platform principal id, never TenantId/TenantUserId. The access token is returned in the body; the refresh
// token and CSRF token are Set-Cookie only (HttpOnly refresh, readable CSRF), mirroring the tenant surface.
public sealed record PlatformAuthenticatedResponse(
  string Outcome,
  string TokenType,
  string AccessToken,
  DateTimeOffset AccessTokenExpiresUtc,
  long PlatformSupportPrincipalId,
  long AuthenticationSessionId);
