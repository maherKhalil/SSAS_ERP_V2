using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record VerifyPasswordCredentialsCommand(string LoginEmail, string Password)
{
  public override string ToString() => "VerifyPasswordCredentialsCommand { LoginEmail = [REDACTED], Password = [REDACTED] }";

  private static string DebuggerDisplay => "VerifyPasswordCredentialsCommand [REDACTED]";
}
