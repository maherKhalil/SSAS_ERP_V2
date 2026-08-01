using System.Diagnostics;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Authentication;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SensitiveAccessToken
{
  private string? value;

  public SensitiveAccessToken(string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    this.value = value;
  }

  public bool IsAvailable => value is not null;

  public Result<string> RevealOnce()
  {
    if (value is null)
    {
      return Result.Failure<string>(AuthenticationErrors.SensitiveTokenConsumed);
    }

    var revealed = value;
    value = null;
    return Result.Success(revealed);
  }

  public override string ToString() => "[REDACTED SENSITIVE ACCESS TOKEN]";

  private static string DebuggerDisplay => "[REDACTED SENSITIVE ACCESS TOKEN]";
}
