namespace SSAS.Host.API.Authentication;

public sealed class AuthenticationTransportOptions
{
  public const string SectionName = "AuthenticationTransport";
  public string[] AllowedOrigins { get; init; } = [];
  public string ProxyMode { get; init; } = "Direct";
  public string[] KnownProxies { get; init; } = [];
  public string[] KnownNetworks { get; init; } = [];
  public string RateLimitHmacSecret { get; init; } = string.Empty;
  public bool UpstreamDistributedRateLimitingEnforced { get; init; }
  public string DataProtectionKeyRingPath { get; init; } = string.Empty;
  public string DataProtectionCertificatePath { get; init; } = string.Empty;
  public string DataProtectionCertificatePassword { get; init; } = string.Empty;
}
