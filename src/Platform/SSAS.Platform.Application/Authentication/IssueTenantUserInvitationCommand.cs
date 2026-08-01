using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record IssueTenantUserInvitationCommand(string LoginEmail, string DisplayName)
{
  public override string ToString() =>
    "IssueTenantUserInvitationCommand { LoginEmail = [REDACTED], DisplayName = [REDACTED] }";

  private static string DebuggerDisplay => "IssueTenantUserInvitationCommand [REDACTED]";
}
