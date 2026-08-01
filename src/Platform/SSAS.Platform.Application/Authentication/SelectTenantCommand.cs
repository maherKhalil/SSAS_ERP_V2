using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record SelectTenantCommand(
  SensitiveAuthenticationTokenInput SelectionProof,
  AuthenticationClientId ClientId,
  long TenantUserId,
  Guid TenantId)
{
  public override string ToString() =>
    $"SelectTenantCommand {{ SelectionProof = [REDACTED], ClientId = {ClientId}, TenantUserId = {TenantUserId}, TenantId = {TenantId} }}";

  private static string DebuggerDisplay => "SelectTenantCommand [SENSITIVE PROOF REDACTED]";
}
