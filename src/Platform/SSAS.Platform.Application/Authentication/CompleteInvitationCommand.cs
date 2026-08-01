using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record CompleteInvitationCommand(string ActionToken, string? InitialPassword)
{
  public override string ToString() => "CompleteInvitationCommand { ActionToken = [REDACTED], InitialPassword = [REDACTED] }";

  private static string DebuggerDisplay => "CompleteInvitationCommand [REDACTED]";
}
