using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using SSAS.Platform.API.Authentication;

namespace SSAS.Host.API.Authentication;

public sealed class AuthenticationRequestSecurity(IOptions<AuthenticationTransportOptions> optionsAccessor)
  : IAuthenticationRequestSecurity
{
  private readonly HashSet<string> allowedOrigins = optionsAccessor.Value.AllowedOrigins.ToHashSet(StringComparer.Ordinal);

  public bool IsAccepted(HttpContext context, bool requireJson)
  {
    if (!context.Request.IsHttps || (requireJson && !context.Request.HasJsonContentType())) return false;
    var origin = context.Request.Headers.Origin;
    if (origin.Count == 1 && allowedOrigins.Contains(origin[0]!)) return true;
    if (origin.Count != 0) return false;
    if (!context.Request.Headers.TryGetValue("Referer", out var referer) || referer.Count != 1 ||
      !Uri.TryCreate(referer[0], UriKind.Absolute, out var uri)) return false;
    return allowedOrigins.Contains(uri.GetLeftPart(UriPartial.Authority));
  }
}

public sealed class AuthenticationEndpointRateLimiter : IAuthenticationEndpointRateLimiter
{
  private readonly byte[] hmacKey;
  private readonly ConcurrentDictionary<string, Window> windows = new(StringComparer.Ordinal);

  public AuthenticationEndpointRateLimiter(IOptions<AuthenticationTransportOptions> options, IHostEnvironment environment)
  {
    hmacKey = string.IsNullOrWhiteSpace(options.Value.RateLimitHmacSecret) && environment.IsDevelopment()
      ? RandomNumberGenerator.GetBytes(32)
      : Encoding.UTF8.GetBytes(options.Value.RateLimitHmacSecret);
  }

  public ValueTask<AuthenticationRateLimitResult> AcquireAsync(
    AuthenticationEndpointKind endpoint, HttpContext context, string partitionMaterial,
    CancellationToken cancellationToken = default)
  {
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var now = DateTimeOffset.UtcNow;
    AuthenticationRateLimitResult result;
    if (endpoint == AuthenticationEndpointKind.Login)
    {
      result = Acquire(Hash("login-ip\0" + ip), 30, TimeSpan.FromMinutes(1), now);
      if (!result.Allowed) return ValueTask.FromResult(result);
      result = Acquire(Hash("login-identity\0" + partitionMaterial.Trim().ToUpperInvariant() + "\0" + ip),
        5, TimeSpan.FromMinutes(15), now);
    }
    else
    {
      var (limit, duration) = endpoint switch
      {
        AuthenticationEndpointKind.TenantSelection => (10, TimeSpan.FromMinutes(5)),
        AuthenticationEndpointKind.Refresh => (10, TimeSpan.FromMinutes(1)),
        AuthenticationEndpointKind.Logout => (5, TimeSpan.FromMinutes(1)),
        _ => (1, TimeSpan.FromMinutes(1))
      };
      result = Acquire(Hash(endpoint + "\0" + partitionMaterial + "\0" + ip), limit, duration, now);
    }
    return ValueTask.FromResult(result);
  }

  private AuthenticationRateLimitResult Acquire(string key, int limit, TimeSpan duration, DateTimeOffset now)
  {
    var window = windows.GetOrAdd(key, _ => new Window());
    lock (window.Sync)
    {
      while (window.Requests.TryPeek(out var oldest) && oldest <= now - duration) window.Requests.Dequeue();
      if (window.Requests.Count >= limit)
        return new AuthenticationRateLimitResult(false, window.Requests.Peek().Add(duration) - now);
      window.Requests.Enqueue(now);
      return AuthenticationRateLimitResult.Permit;
    }
  }

  private string Hash(string material)
  {
    using var hmac = new HMACSHA256(hmacKey);
    return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(material)));
  }

  private sealed class Window
  {
    public object Sync { get; } = new();
    public Queue<DateTimeOffset> Requests { get; } = new();
  }
}

public static class AuthenticationTransportServiceCollectionExtensions
{
  public const string CorsPolicy = "AuthenticationExactOrigins";

  public static IServiceCollection AddHostAuthenticationTransport(
    this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
  {
    var options = configuration.GetSection(AuthenticationTransportOptions.SectionName)
      .Get<AuthenticationTransportOptions>() ?? new AuthenticationTransportOptions();
    Validate(options, environment);
    services.AddOptions<AuthenticationTransportOptions>()
      .BindConfiguration(AuthenticationTransportOptions.SectionName).ValidateOnStart();
    services.AddSingleton<IAuthenticationRequestSecurity, AuthenticationRequestSecurity>();
    services.AddSingleton<IAuthenticationEndpointRateLimiter, AuthenticationEndpointRateLimiter>();
    services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy => policy
      .WithOrigins(options.AllowedOrigins).AllowCredentials().AllowAnyMethod().AllowAnyHeader()));

    var dataProtection = services.AddDataProtection().SetApplicationName("SSAS.ERP");
    if (environment.IsDevelopment())
    {
      dataProtection.UseEphemeralDataProtectionProvider();
    }
    else
    {
      var certificate = new X509Certificate2(options.DataProtectionCertificatePath,
        options.DataProtectionCertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
      var now = DateTimeOffset.UtcNow;
      if (!certificate.HasPrivateKey || now < certificate.NotBefore || now > certificate.NotAfter)
        throw new InvalidOperationException("The Data Protection certificate must have a currently valid private key.");
      var writeProbe = Path.Combine(options.DataProtectionKeyRingPath, $".ssas-write-probe-{Guid.NewGuid():N}");
      using (new FileStream(writeProbe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
      dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.DataProtectionKeyRingPath))
        .ProtectKeysWithCertificate(certificate);
    }
    return services;
  }

  public static void ConfigureTrustedForwarding(this WebApplication app, IConfiguration configuration)
  {
    var options = configuration.GetSection(AuthenticationTransportOptions.SectionName)
      .Get<AuthenticationTransportOptions>() ?? new AuthenticationTransportOptions();
    if (!string.Equals(options.ProxyMode, "TrustedProxy", StringComparison.Ordinal)) return;
    var forwarded = new ForwardedHeadersOptions
    {
      ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
      ForwardLimit = 1
    };
    forwarded.KnownProxies.Clear();
    forwarded.KnownNetworks.Clear();
    foreach (var value in options.KnownProxies) forwarded.KnownProxies.Add(IPAddress.Parse(value));
    foreach (var value in options.KnownNetworks)
    {
      var parts = value.Split('/', 2);
      forwarded.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
        IPAddress.Parse(parts[0]), int.Parse(parts[1], CultureInfo.InvariantCulture)));
    }
    app.UseForwardedHeaders(forwarded);
  }

  private static void Validate(AuthenticationTransportOptions options, IHostEnvironment environment)
  {
    if (options.AllowedOrigins.Length == 0) throw new InvalidOperationException("At least one exact authentication Origin is required.");
    foreach (var origin in options.AllowedOrigins)
    {
      if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
        uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
        !string.IsNullOrEmpty(uri.UserInfo) || origin.Contains('*') ||
        !string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
        throw new InvalidOperationException("Authentication origins must be exact HTTPS origins without paths, queries, fragments, or wildcards.");
    }
    if (options.AllowedOrigins.Distinct(StringComparer.Ordinal).Count() != options.AllowedOrigins.Length)
      throw new InvalidOperationException("Authentication origins must not contain duplicates.");
    if (options.ProxyMode is not ("Direct" or "TrustedProxy")) throw new InvalidOperationException("ProxyMode must be Direct or TrustedProxy.");
    if (options.ProxyMode == "TrustedProxy" && options.KnownProxies.Length == 0 && options.KnownNetworks.Length == 0)
      throw new InvalidOperationException("TrustedProxy mode requires explicit known proxies or networks.");
    if (!environment.IsDevelopment())
    {
      if (options.RateLimitHmacSecret.Length < 32 || !options.UpstreamDistributedRateLimitingEnforced)
        throw new InvalidOperationException("Production requires a rate-limit HMAC secret and declared upstream distributed enforcement.");
      if (string.IsNullOrWhiteSpace(options.DataProtectionKeyRingPath) ||
        string.IsNullOrWhiteSpace(options.DataProtectionCertificatePath) ||
        !Directory.Exists(options.DataProtectionKeyRingPath) || !File.Exists(options.DataProtectionCertificatePath))
        throw new InvalidOperationException("Production Data Protection key-ring and certificate material are required.");
    }
  }
}
