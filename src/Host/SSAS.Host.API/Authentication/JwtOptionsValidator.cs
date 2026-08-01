using Microsoft.Extensions.Options;

namespace SSAS.Host.API.Authentication;

public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
  public ValidateOptionsResult Validate(string? name, JwtOptions options)
  {
    var failures = new List<string>();
    if (string.IsNullOrWhiteSpace(options.Issuer)) failures.Add("Jwt:Issuer is required.");
    if (string.IsNullOrWhiteSpace(options.Audience)) failures.Add("Jwt:Audience is required.");
    if (options.AccessTokenLifetime <= TimeSpan.Zero || options.AccessTokenLifetime > TimeSpan.FromMinutes(15))
      failures.Add("Jwt:AccessTokenLifetime must be positive and no greater than 15 minutes.");
    if (options.ClockSkewSeconds != 30) failures.Add("Jwt:ClockSkewSeconds must be 30.");
    if (options.MaximumEncodedTokenSize != 8192) failures.Add("Jwt:MaximumEncodedTokenSize must be 8192.");
    if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.ActiveSigningCertificatePath))
      failures.Add("Jwt:ActiveSigningCertificatePath is required outside Development.");

    return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
  }
}
