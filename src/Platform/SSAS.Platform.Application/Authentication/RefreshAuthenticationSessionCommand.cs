using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record RefreshAuthenticationSessionCommand(
  SensitiveAuthenticationTokenInput RefreshToken,
  AuthenticationClientId ClientId)
{
  public override string ToString() =>
    $"RefreshAuthenticationSessionCommand {{ RefreshToken = [REDACTED], ClientId = {ClientId} }}";

  private static string DebuggerDisplay => "RefreshAuthenticationSessionCommand [SENSITIVE TOKEN REDACTED]";
}
