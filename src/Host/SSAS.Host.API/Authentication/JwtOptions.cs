namespace SSAS.Host.API.Authentication;

public sealed class JwtOptions
{
  public const string SectionName = "Jwt";

  public string Issuer { get; init; } = string.Empty;

  public string Audience { get; init; } = string.Empty;

  public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

  public int ClockSkewSeconds { get; init; } = 30;

  public int MaximumEncodedTokenSize { get; init; } = 8192;

  public string ActiveSigningCertificatePath { get; init; } = string.Empty;

  public string ActiveSigningCertificatePassword { get; init; } = string.Empty;

  public VerificationCertificateOptions[] VerificationCertificates { get; init; } = [];
}

public sealed class VerificationCertificateOptions
{
  public string Path { get; init; } = string.Empty;

  public bool Enabled { get; init; } = true;

  public DateTimeOffset? RetireAfterUtc { get; init; }
}
