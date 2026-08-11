namespace SSAS.Platform.Application.Abstractions.Persistence;

// Result of resolving a platform refresh-token public id to its owning platform session
// (ADR-016 Phase 3C / DEC-TEN-0022). Carries no tenant identifiers; the platform plane is owned by the
// persistence store the locator was resolved from, never by a request parameter.
public sealed record PlatformRefreshTokenSessionLocator(
  long PlatformAuthenticationSessionId,
  long IdentityId,
  long PlatformSupportPrincipalId);
