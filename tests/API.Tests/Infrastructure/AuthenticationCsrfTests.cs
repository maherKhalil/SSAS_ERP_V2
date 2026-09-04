using System.Globalization;
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
  [Trait("Criterion", "AC-AUTH-0042")]
  // ==================================================================================================
  // `AC-AUTH-0042`, QUOTED TO ITS TERMINAL FULL STOP — *"Refresh and logout REQUIRE the exact
  // Data-Protection-signed CSRF cookie/header pair bound to current session, refresh selector, and
  // ClientId; state rotates and clears with refresh state and production key-ring startup fails closed."*
  // ==================================================================================================
  //   the exact PAIR         this test: header tampered by one character, header absent, cookie empty —
  //                          each refused. Both halves must be present AND equal.
  //   Data-Protection-signed the value is produced by a time-limited protector and the tampered value is
  //                          refused; `Csrf_rejects_...` also forges a payload with the real protector
  //   refresh selector       `Csrf_rejects_...` presents a well-formed value against a DIFFERENT token
  //   ClientId               the same test protects a payload carrying `wrong-client`
  //   state rotates          `Assert.NotEqual(first, rotated)` — two `Create` calls with identical inputs
  //   clears with refresh    `Refresh_and_csrf_cookie_creation_and_deletion_...` clears both cookies with
  //                          one helper
  //   key-ring fails closed  `Production_transport_without_shared_rate_limit_and_data_protection_
  //                          configuration_fails_startup`
  //
  // ⚠⚠⚠ TWO CLAUSES HAVE NO WITNESS AT COMPILE SCOPE, AND BOTH WERE SETTLED BY DELETING THE MECHANISM
  // RATHER THAN BY SEARCHING FOR THE ASSERTION — a name search cannot establish that nothing tests a thing.
  //
  //   *REFRESH AND LOGOUT **REQUIRE** THE PAIR.* Everything above tests `AuthenticationCsrfService` in
  //   isolation: it proves the service REFUSES, never that either endpoint ASKS it. Plant: neutering the
  //   CSRF guard in the refresh route (`... && false`, so `TryValidate` still runs and its answer is
  //   discarded) left all seven suites green — **all 3296 compile-scope tests pass with the refresh route's CSRF check
  //   inoperative.**
  //
  //   *BOUND TO CURRENT SESSION.* `TryValidate` RETURNS the session id; it does not take one to compare
  //   against, so the binding cannot live in this file at all — it is the caller's comparison. ⚠ And the
  //   two routes differ by necessity: logout compares `currentSession...AuthenticationSessionId` against
  //   the payload, and **refresh cannot, because it is anonymous — there is no current session at refresh
  //   time, which is the point of refreshing.** On refresh the binding is the SELECTOR, tested above.
  //   Plant: deleting logout's comparison left all seven suites green.
  //
  // ⚠ NEITHER PLANT IS EVIDENCE ABOUT `tests/Integration.Tests/`, which `GATE_SCOPE=TASK` compiles and does
  // not run. The claim is bounded to the seven compile-scope suites, and the SQL-backed suites are the
  // likeliest home for a route-level witness.
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
  [Trait("Criterion", "AC-AUTH-0042")]
  // `AC-AUTH-0042`'s REFUSAL half — five distinct rejections plus rotation, clause map on
  // `Protected_csrf_value_requires_exact_cookie_header_selector_and_session_binding`.
  //
  // ⚠ THE EXPIRED AND WRONG-CLIENT ROWS FORGE THEIR PAYLOAD WITH THE REAL PROTECTOR AND THE REAL PURPOSE
  // STRING (`SSAS.ERP.Authentication.Csrf.v1`). **That is what makes them test the PAYLOAD rather than the
  // signature**: a corrupted blob is refused by the data-protection layer and would prove nothing about
  // whether anyone reads `ExpiresUnixTimeSeconds` or `ClientId`. Each row is correctly signed and wrong in
  // exactly one field.
  //
  // ⚠⚠ WHICH MAKES THE PURPOSE STRING A SILENT COUPLING: it is duplicated here from the service, and if the
  // service changes it, these two rows stop being forgeries and become unreadable blobs — **still refused,
  // still green, and no longer testing the field they name.** A refusal test cannot notice that its input
  // stopped being the input it meant to construct.
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
  [Trait("Criterion", "AC-AUTH-0042")]
  // `AC-AUTH-0042`'s last clause — *"production key-ring startup fails closed."* The only clause of this
  // criterion that is about STARTUP rather than a request, and therefore the only one a request-shaped
  // fixture could never have reached.
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
  [Trait("Criterion", "AC-AUTH-0041")]
  // ==================================================================================================
  // `AC-AUTH-0041`, QUOTED TO ITS TERMINAL FULL STOP — *"The exact refresh cookie is Secure, HttpOnly,
  // SameSite Strict, host-only, scoped to `/api/platform/auth`, expiry-aligned, rotated on refresh, and
  // cleared with identical attributes on logout or terminal refresh failure."* Nine clauses:
  // ==================================================================================================
  //   Secure / SameSite Strict / path   `AssertCookie`, on all four cookies
  //   HttpOnly                          asserted as an EQUALITY, not a containment — the CSRF cookie must
  //                                     NOT be HttpOnly (the browser has to read it) and the refresh
  //                                     cookie must be. A `Contains` would have passed both ways round.
  //   host-only                         `DoesNotContain("domain=")` — here the ABSENCE is the attribute
  //   cleared with identical attributes  creation and deletion go through the SAME helper; sharing the
  //                                     assertion is what makes *identical* a claim rather than two lists
  //   EXPIRY-ALIGNED                    see below
  //   rotated on refresh                ⚠ NOT WITNESSED HERE, and this test cannot witness it: it calls
  //                                     `WriteCookies` directly and never performs a refresh.
  //                                     `Csrf_rejects_missing_...and_rotates` pins that two `Create` calls
  //                                     differ, which is rotation of the CSRF VALUE, not of the cookie
  //                                     across a refresh round-trip. Named as the gap it is.
  //   on logout OR TERMINAL REFRESH     ⚠ `ClearCookies` is called directly, so this proves the clearing
  //   FAILURE                           SHAPE, not that either caller invokes it. The two call sites are
  //                                     the endpoint's business.
  //
  // ⚠⚠ *EXPIRY-ALIGNED* WAS ASSERTED AS `Assert.Contains("expires=")` — THE ATTRIBUTE'S PRESENCE, NOT ITS
  // VALUE. A cookie outliving its refresh token by a year contains `expires=`. **The criterion's word is
  // ALIGNED, and alignment is a relation between two values; a containment check cannot express a relation
  // at all, only that one side of it exists.** The expected instant is now passed in and compared.
  //
  // ⚠ Same family as the hard-coded 2048-bit fixture, approached from the other side: there a literal at
  // the threshold meant no fixture could contradict it; here the value was never read, so no assertion
  // could. **Both leave a criterion's quantity outside the test space while the file reads as thorough,
  // and both are invisible to a reader who checks that the criterion is mentioned.**
  public void Refresh_and_csrf_cookie_creation_and_deletion_use_exact_matching_attributes()
  {
    var services = new ServiceCollection();
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    using var provider = services.BuildServiceProvider();
    var csrf = new AuthenticationCsrfService(provider.GetRequiredService<IDataProtectionProvider>());
    var refreshToken = $"{Guid.NewGuid():N}.{new string('A', 43)}";
    var context = new DefaultHttpContext();
    var refreshExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30);

    InvokeEndpointCookieMethod("WriteCookies", context, refreshToken, 42L, refreshExpiresUtc, csrf);
    var created = context.Response.Headers.SetCookie.Select(value => value!).ToArray();
    Assert.Equal(2, created.Length);
    AssertCookie(created.Single(value => value.StartsWith("__Secure-ssas-refresh=", StringComparison.Ordinal)), true, false, refreshExpiresUtc);
    AssertCookie(created.Single(value => value.StartsWith("__Secure-ssas-xsrf=", StringComparison.Ordinal)), false, false, refreshExpiresUtc);

    context = new DefaultHttpContext();
    InvokeEndpointCookieMethod("ClearCookies", context);
    var deleted = context.Response.Headers.SetCookie.Select(value => value!).ToArray();
    Assert.Equal(2, deleted.Length);
    AssertCookie(deleted.Single(value => value.StartsWith("__Secure-ssas-refresh=", StringComparison.Ordinal)), true, true, DateTimeOffset.UnixEpoch);
    AssertCookie(deleted.Single(value => value.StartsWith("__Secure-ssas-xsrf=", StringComparison.Ordinal)), false, true, DateTimeOffset.UnixEpoch);
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

  private static void AssertCookie(string value, bool httpOnly, bool deleted, DateTimeOffset expectedExpiry)
  {
    Assert.Contains("path=/api/platform/auth", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("samesite=strict", value, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("max-age=", value, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("domain=", value, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(httpOnly, value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    Assert.Equal(deleted, value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase));
    // The expiry is READ, not merely found. `Assert.Contains("expires=")` passed for any instant whatever,
    // so *expiry-aligned* was carried entirely by the attribute existing.
    Assert.Equal(TruncateToSecond(expectedExpiry), ParseCookieExpiry(value));
  }

  // `Set-Cookie` carries an RFC 1123 date, so the comparison is at second resolution. The truncation is
  // applied to the EXPECTED side only — never to the parsed side — so a cookie whose own value has been
  // rounded or shifted still fails rather than being normalised into agreement.
  private static DateTimeOffset TruncateToSecond(DateTimeOffset value) =>
    new(value.UtcDateTime.AddTicks(-(value.UtcTicks % TimeSpan.TicksPerSecond)), TimeSpan.Zero);

  private static DateTimeOffset ParseCookieExpiry(string setCookieValue)
  {
    var start = setCookieValue.IndexOf("expires=", StringComparison.OrdinalIgnoreCase);
    Assert.True(start >= 0,
      $"no expires attribute in '{setCookieValue}'; the alignment check has nothing to read.");
    var rest = setCookieValue[(start + "expires=".Length)..];
    var end = rest.IndexOf(';');
    var text = (end >= 0 ? rest[..end] : rest).Trim();
    return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture,
      DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
  }

  private sealed class TestHostEnvironment : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "SSAS.API.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
