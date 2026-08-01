using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record CompletePasswordResetCommand(string ActionToken, string NewPassword)
{
  public override string ToString() => "CompletePasswordResetCommand { ActionToken = [REDACTED], NewPassword = [REDACTED] }";

  private static string DebuggerDisplay => "CompletePasswordResetCommand [REDACTED]";
}
