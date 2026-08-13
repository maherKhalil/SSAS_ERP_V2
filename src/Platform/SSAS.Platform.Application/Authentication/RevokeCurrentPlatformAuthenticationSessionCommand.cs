namespace SSAS.Platform.Application.Authentication;

// Platform-plane current-session logout (Phase 4B / DEC-TEN-0023). Both fields are bound from the caller's
// already-validated platform access token (the trusted session_id and identity_id claims) by the transport
// layer — never from request body/query. The caller can therefore only revoke its own current platform session.
public sealed record RevokeCurrentPlatformAuthenticationSessionCommand(
  long PlatformAuthenticationSessionId,
  long IdentityId);
