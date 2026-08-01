namespace SSAS.Platform.Application.Authentication;

public sealed class PasswordResetIssuanceResult
{
  private PasswordResetIssuanceResult(SensitiveActionToken? sensitiveToken)
  {
    SensitiveToken = sensitiveToken;
  }

  public SensitiveActionToken? SensitiveToken { get; }

  public static PasswordResetIssuanceResult Accepted() => new(null);

  public static PasswordResetIssuanceResult Accepted(SensitiveActionToken sensitiveToken) => new(sensitiveToken);
}
