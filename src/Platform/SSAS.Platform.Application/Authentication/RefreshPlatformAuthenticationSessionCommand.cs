using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

// Platform-plane refresh input (ADR-016 Phase 3C / DEC-TEN-0022). Carries only the opaque platform refresh
// token and the client id — no plane selector. The token resolves ONLY against platform-session persistence.
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record RefreshPlatformAuthenticationSessionCommand(
  SensitiveAuthenticationTokenInput RefreshToken,
  AuthenticationClientId ClientId)
{
  public override string ToString() =>
    $"RefreshPlatformAuthenticationSessionCommand {{ RefreshToken = [REDACTED], ClientId = {ClientId} }}";

  private static string DebuggerDisplay => "RefreshPlatformAuthenticationSessionCommand [SENSITIVE TOKEN REDACTED]";
}
