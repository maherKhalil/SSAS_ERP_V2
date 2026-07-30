using Microsoft.Extensions.Options;

namespace SSAS.Host.API.Authentication;

public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
  private const string DevelopmentOnlySigningKeyPrefix = "DevelopmentOnlySigningKey";

  public ValidateOptionsResult Validate(string? name, JwtOptions options)
  {
    var failures = new List<string>();

    if (string.IsNullOrWhiteSpace(options.Issuer))
    {
      failures.Add("Jwt:Issuer is required.");
    }

    if (string.IsNullOrWhiteSpace(options.Audience))
    {
      failures.Add("Jwt:Audience is required.");
    }

    if (options.SigningKey.Length < 32)
    {
      failures.Add("Jwt:SigningKey must contain at least 32 characters.");
    }

    if (!environment.IsDevelopment() && options.SigningKey.StartsWith(DevelopmentOnlySigningKeyPrefix, StringComparison.Ordinal))
    {
      failures.Add("The development-only JWT signing key cannot be used outside Development.");
    }

    if (options.ClockSkewSeconds is < 0 or > 300)
    {
      failures.Add("Jwt:ClockSkewSeconds must be between 0 and 300.");
    }

    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }
}
