using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSAS.Host.API.Authentication;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain;

namespace SSAS.API.Tests.Infrastructure;

public sealed class AuthenticationCsrfTests
{
  [Fact]
  public void Protected_csrf_value_requires_exact_cookie_header_selector_and_session_binding()
  {
    var services = new ServiceCollection();
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    using var provider = services.BuildServiceProvider();
    var csrf = new AuthenticationCsrfService(provider.GetRequiredService<IDataProtectionProvider>());
    var publicId = Guid.Parse("e78b805d-ca17-4f9f-bd24-0c10b355f9a1");
    var refreshToken = $"{publicId:N}.{new string('A', 43)}";
    var protectedValue = csrf.Create(refreshToken, 42, DateTimeOffset.UtcNow.AddMinutes(30).AddTicks(1234));
    var context = new DefaultHttpContext();
    context.Request.Headers.Cookie = $"{AuthenticationCsrfService.CookieName}={protectedValue}";
    context.Request.Headers[AuthenticationCsrfService.HeaderName] = protectedValue;

    Assert.True(csrf.TryValidate(context, refreshToken, out var payload));
    Assert.Equal(42, payload.AuthenticationSessionId);
    Assert.Equal(publicId, payload.RefreshTokenPublicId);

    context.Request.Headers[AuthenticationCsrfService.HeaderName] = protectedValue + "x";
    Assert.False(csrf.TryValidate(context, refreshToken, out _));
  }

  [Fact]
  public void Csrf_rejects_missing_malformed_expired_wrong_selector_and_wrong_client_values_and_rotates()
  {
    var services = new ServiceCollection();
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    using var provider = services.BuildServiceProvider();
    var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
    var csrf = new AuthenticationCsrfService(dataProtection);
    var publicId = Guid.Parse("e78b805d-ca17-4f9f-bd24-0c10b355f9a1");
    var refreshToken = $"{publicId:N}.{new string('A', 43)}";
    var expires = DateTimeOffset.UtcNow.AddMinutes(30);
    var first = csrf.Create(refreshToken, 42, expires);
    var rotated = csrf.Create(refreshToken, 42, expires);
    Assert.NotEqual(first, rotated);

    var context = Context(first);
    context.Request.Headers.Remove(AuthenticationCsrfService.HeaderName);
    Assert.False(csrf.TryValidate(context, refreshToken, out _));
    context = Context(first);
    context.Request.Headers.Cookie = string.Empty;
    Assert.False(csrf.TryValidate(context, refreshToken, out _));
    context = Context("malformed");
    Assert.False(csrf.TryValidate(context, refreshToken, out _));
    context = Context(first);
    var wrongSelector = $"{Guid.NewGuid():N}.{new string('A', 43)}";
    Assert.False(csrf.TryValidate(context, wrongSelector, out _));

    var protector = dataProtection.CreateProtector("SSAS.ERP.Authentication.Csrf.v1").ToTimeLimitedDataProtector();
    var expiredAt = DateTimeOffset.UtcNow.AddSeconds(-1);
    var expired = protector.Protect(Payload(publicId, 42, AuthenticationClientId.V1Web, expiredAt), expiredAt);
    Assert.False(csrf.TryValidate(Context(expired), refreshToken, out _));
    var wrongClient = protector.Protect(Payload(publicId, 42, "wrong-client", expires), expires);
    Assert.False(csrf.TryValidate(Context(wrongClient), refreshToken, out _));
  }

  [Fact]
  public void Authentication_request_security_requires_https_and_an_exact_configured_origin()
  {
    var security = new AuthenticationRequestSecurity(Options.Create(new AuthenticationTransportOptions
    {
      AllowedOrigins = ["https://app.example.test"]
    }));
    var context = new DefaultHttpContext();
    context.Request.Scheme = "https";
    context.Request.Headers.Origin = "https://app.example.test";

    Assert.True(security.IsAccepted(context, false));
    context.Request.Headers.Origin = "https://other.example.test";
    Assert.False(security.IsAccepted(context, false));
    context.Request.Headers.Origin = "https://app.example.test";
    context.Request.Scheme = "http";
    Assert.False(security.IsAccepted(context, false));
  }

  [Fact]
  public async Task Login_and_logout_limits_reject_without_queueing_after_the_approved_counts()
  {
    var limiter = new AuthenticationEndpointRateLimiter(Options.Create(new AuthenticationTransportOptions()),
      new TestHostEnvironment());
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

    for (var index = 0; index < 5; index++)
      Assert.True((await limiter.AcquireAsync(AuthenticationEndpointKind.Login, context, "user@example.test")).Allowed);
    Assert.False((await limiter.AcquireAsync(AuthenticationEndpointKind.Login, context, "user@example.test")).Allowed);

    for (var index = 0; index < 5; index++)
      Assert.True((await limiter.AcquireAsync(AuthenticationEndpointKind.Logout, context, "42")).Allowed);
    var rejected = await limiter.AcquireAsync(AuthenticationEndpointKind.Logout, context, "42");
    Assert.False(rejected.Allowed);
    Assert.True(rejected.RetryAfter > TimeSpan.Zero);
  }

  [Theory]
  [InlineData(AuthenticationEndpointKind.TenantSelection, 10)]
  [InlineData(AuthenticationEndpointKind.Refresh, 10)]
  [InlineData(AuthenticationEndpointKind.Logout, 5)]
  public async Task Partitioned_endpoint_limits_reject_the_first_request_above_the_exact_limit(
    AuthenticationEndpointKind endpoint,
    int limit)
  {
    var limiter = NewLimiter();
    var context = NewRateLimitContext();

    for (var index = 0; index < limit; index++)
      Assert.True((await limiter.AcquireAsync(endpoint, context, "sensitive-partition-material")).Allowed);
    var rejected = await limiter.AcquireAsync(endpoint, context, "sensitive-partition-material");

    Assert.False(rejected.Allowed);
    Assert.True(rejected.RetryAfter > TimeSpan.Zero);
  }

  [Fact]
  public async Task Login_enforces_both_identity_and_trusted_ip_limits()
  {
    var context = NewRateLimitContext();
    var identityLimiter = NewLimiter();
    for (var index = 0; index < 5; index++)
      Assert.True((await identityLimiter.AcquireAsync(AuthenticationEndpointKind.Login, context, "user@example.test")).Allowed);
    Assert.False((await identityLimiter.AcquireAsync(AuthenticationEndpointKind.Login, context, "user@example.test")).Allowed);

    var ipLimiter = NewLimiter();
    for (var index = 0; index < 30; index++)
      Assert.True((await ipLimiter.AcquireAsync(AuthenticationEndpointKind.Login, context, $"user{index}@example.test")).Allowed);
    Assert.False((await ipLimiter.AcquireAsync(AuthenticationEndpointKind.Login, context, "overflow@example.test")).Allowed);
  }

  [Fact]
  public async Task Exact_cors_policy_allows_credentials_only_for_configured_https_origin()
  {
    var configuration = Configuration("Direct");
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddHostAuthenticationTransport(configuration, new TestHostEnvironment());
    using var provider = services.BuildServiceProvider();
    var policy = await provider.GetRequiredService<ICorsPolicyProvider>()
      .GetPolicyAsync(new DefaultHttpContext(), AuthenticationTransportServiceCollectionExtensions.CorsPolicy);

    Assert.NotNull(policy);
    Assert.True(policy.SupportsCredentials);
    Assert.Equal(["https://app.example.test"], policy.Origins);
    Assert.DoesNotContain("*", policy.Origins);
  }

  [Theory]
  [InlineData("*")]
  [InlineData("https://*.example.test")]
  [InlineData("http://app.example.test")]
  [InlineData("https://app.example.test/path")]
  [InlineData("https://app.example.test?query=1")]
  public void Invalid_origin_configuration_fails_startup(string origin)
  {
    var configuration = Configuration("Direct", origin);
    var services = new ServiceCollection();

    Assert.Throws<InvalidOperationException>(() =>
      services.AddHostAuthenticationTransport(configuration, new TestHostEnvironment()));
  }

  [Fact]
  public void Trusted_proxy_mode_without_an_explicit_proxy_or_network_fails_startup()
  {
    var services = new ServiceCollection();

    Assert.Throws<InvalidOperationException>(() =>
      services.AddHostAuthenticationTransport(Configuration("TrustedProxy"), new TestHostEnvironment()));
  }

  [Fact]
  public void Production_transport_without_shared_rate_limit_and_data_protection_configuration_fails_startup()
  {
    var services = new ServiceCollection();

    Assert.Throws<InvalidOperationException>(() => services.AddHostAuthenticationTransport(
      Configuration("Direct"), new TestHostEnvironment { EnvironmentName = Environments.Production }));
  }

  [Theory]
  [InlineData("Direct", null, "127.0.0.1|http")]
  [InlineData("TrustedProxy", "127.0.0.1", "203.0.113.10|https")]
  [InlineData("TrustedProxy", "10.0.0.1", "127.0.0.1|http")]
  public async Task Forwarded_headers_are_used_only_from_an_explicit_trusted_proxy(
    string proxyMode,
    string? knownProxy,
    string expected)
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddConfiguration(Configuration(proxyMode, knownProxy: knownProxy));
    await using var app = builder.Build();
    app.Use((context, next) =>
    {
      context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
      return next();
    });
    app.ConfigureTrustedForwarding(builder.Configuration);
    app.MapGet("/client", (HttpContext context) => $"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}");
    await app.StartAsync();
    using var request = new HttpRequestMessage(HttpMethod.Get, "/client");
    request.Headers.Add("X-Forwarded-For", "203.0.113.10");
    request.Headers.Add("X-Forwarded-Proto", "https");

    var response = await app.GetTestClient().SendAsync(request);

    Assert.Equal(expected, await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public void Refresh_and_csrf_cookie_creation_and_deletion_use_exact_matching_attributes()
  {
    var services = new ServiceCollection();
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    using var provider = services.BuildServiceProvider();
    var csrf = new AuthenticationCsrfService(provider.GetRequiredService<IDataProtectionProvider>());
    var refreshToken = $"{Guid.NewGuid():N}.{new string('A', 43)}";
    var context = new DefaultHttpContext();

    InvokeEndpointCookieMethod("WriteCookies", context, refreshToken, 42L, DateTimeOffset.UtcNow.AddMinutes(30), csrf);
    var created = context.Response.Headers.SetCookie.Select(value => value!).ToArray();
    Assert.Equal(2, created.Length);
    AssertCookie(created.Single(value => value.StartsWith("__Secure-ssas-refresh=", StringComparison.Ordinal)), true, false);
    AssertCookie(created.Single(value => value.StartsWith("__Secure-ssas-xsrf=", StringComparison.Ordinal)), false, false);

    context = new DefaultHttpContext();
    InvokeEndpointCookieMethod("ClearCookies", context);
    var deleted = context.Response.Headers.SetCookie.Select(value => value!).ToArray();
    Assert.Equal(2, deleted.Length);
    AssertCookie(deleted.Single(value => value.StartsWith("__Secure-ssas-refresh=", StringComparison.Ordinal)), true, true);
    AssertCookie(deleted.Single(value => value.StartsWith("__Secure-ssas-xsrf=", StringComparison.Ordinal)), false, true);
  }

  private static AuthenticationEndpointRateLimiter NewLimiter() => new(
    Options.Create(new AuthenticationTransportOptions()), new TestHostEnvironment());

  private static DefaultHttpContext NewRateLimitContext()
  {
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.20");
    return context;
  }

  private static DefaultHttpContext Context(string csrfValue)
  {
    var context = new DefaultHttpContext();
    context.Request.Headers.Cookie = $"{AuthenticationCsrfService.CookieName}={csrfValue}";
    context.Request.Headers[AuthenticationCsrfService.HeaderName] = csrfValue;
    return context;
  }

  private static string Payload(Guid publicId, long sessionId, string clientId, DateTimeOffset expires) =>
    JsonSerializer.Serialize(new
    {
      Version = 1,
      RefreshTokenPublicId = publicId,
      AuthenticationSessionId = sessionId,
      ClientId = clientId,
      Nonce = Guid.NewGuid(),
      ExpiresUnixTimeSeconds = expires.ToUnixTimeSeconds()
    });

  private static IConfiguration Configuration(
    string proxyMode,
    string origin = "https://app.example.test",
    string? knownProxy = null)
  {
    var values = new Dictionary<string, string?>
    {
      ["AuthenticationTransport:AllowedOrigins:0"] = origin,
      ["AuthenticationTransport:ProxyMode"] = proxyMode
    };
    if (knownProxy is not null) values["AuthenticationTransport:KnownProxies:0"] = knownProxy;
    return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
  }

  private static void InvokeEndpointCookieMethod(string name, params object[] arguments)
  {
    var method = typeof(AuthenticationEndpointRouteBuilderExtensions)
      .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);
    method.Invoke(null, arguments);
  }

  private static void AssertCookie(string value, bool httpOnly, bool deleted)
  {
    Assert.Contains("path=/api/platform/auth", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("samesite=strict", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("expires=", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("max-age=", value, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("domain=", value, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(httpOnly, value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    Assert.Equal(deleted, value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase));
  }

  private sealed class TestHostEnvironment : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "SSAS.API.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
