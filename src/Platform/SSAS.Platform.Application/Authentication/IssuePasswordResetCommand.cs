using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record IssuePasswordResetCommand(string LoginEmail)
{
  public override string ToString() => "IssuePasswordResetCommand { LoginEmail = [REDACTED] }";

  private static string DebuggerDisplay => "IssuePasswordResetCommand [REDACTED]";
}
