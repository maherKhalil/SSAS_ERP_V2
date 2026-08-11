namespace SSAS.Platform.Application.Authentication;

// Strongly-typed platform-plane access-token claims (ADR-015 / DEC-TEN-0022). The platform profile is
// expressed by this dedicated type — the issuer stamps security_plane=platform from the type/path, not
// from any field here. It deliberately carries NO TenantId, TenantUserId, CompanyId, Roles, or principal
// status: platform tokens are permission-based and non-tenant. Permissions are catalog-valid PlatformSupport
// names sourced from IPlatformSupportPermissionReadService, never from a caller or bootstrap configuration.
public sealed record PlatformAccessTokenClaims(
  string Subject,
  long IdentityId,
  long AuthenticationSessionId,
  AuthenticationClientId ClientId,
  long SecurityVersion,
  IReadOnlyList<string> Permissions);
