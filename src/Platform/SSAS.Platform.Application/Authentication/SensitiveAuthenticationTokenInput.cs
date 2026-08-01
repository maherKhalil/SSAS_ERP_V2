using System.Diagnostics;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SensitiveAuthenticationTokenInput
{
  private readonly string value;

  public SensitiveAuthenticationTokenInput(string? value)
  {
    this.value = value ?? string.Empty;
  }

  internal string Value => value;

  public override string ToString() => "[REDACTED SENSITIVE AUTHENTICATION TOKEN INPUT]";

  private static string DebuggerDisplay => "[REDACTED SENSITIVE AUTHENTICATION TOKEN INPUT]";
}
