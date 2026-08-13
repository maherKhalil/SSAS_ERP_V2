using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.API;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

// Phase 4B positive end-to-end proof (DEC-TEN-0023). A REAL HTTPS request hits the REAL platform-support auth
// routes on a host wired to REAL platform persistence (SQL Server) and the REAL JWT/DataProtection stack:
// credentials -> VerifyPasswordCredentialsCommandHandler -> PlatformAuthenticationSessionCreator -> issued
// platform JWT + refresh/CSRF cookies -> refresh rotation -> logout revocation. Nothing is mocked or bypassed.
[Collection(PlatformSupportAuthenticationEndToEndGroup.Name)]
public sealed class PlatformSupportAuthenticationEndToEndTests(PlatformSupportAuthenticationEndToEndHost host)
{
  private const string Origin = PlatformSupportAuthenticationEndToEndHost.Origin;
  private const string Password = PlatformSupportAuthenticationEndToEndHost.Password;
  private const string Prefix = "/api/platform/support/auth";
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private static readonly DateTimeOffset Now = new(2026, 8, 12, 11, 0, 0, TimeSpan.Zero);

  // ---- M1 : positive HTTP login (+ M5 issued-JWT validation) ----

  [Fact]
  public async Task Platform_login_issues_a_validated_platform_token_with_refresh_and_csrf_cookies()
  {
    var (email, identityId) = await SeedEligibleOperatorAsync();

    var response = await LoginAsync(email);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await ReadAuthenticatedAsync(response);
    Assert.Equal("Authenticated", body.Outcome);
    Assert.Equal("Bearer", body.TokenType);
    Assert.True(body.PlatformSupportPrincipalId > 0);
    Assert.True(body.AuthenticationSessionId > 0);

    // Cookies: platform refresh (HttpOnly) + CSRF; and NOT the tenant refresh cookie.
    var setCookies = SetCookieHeaders(response);
    var refreshCookie = Assert.Single(setCookies, header => header.StartsWith(PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName + "=", StringComparison.Ordinal));
    Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains($"path={Prefix}", refreshCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains(setCookies, header => header.StartsWith(AuthenticationCsrfService.CookieName + "=", StringComparison.Ordinal));
    Assert.DoesNotContain(setCookies, header =>
      header.StartsWith(AuthenticationEndpointRouteBuilderExtensions.RefreshCookieName + "=", StringComparison.Ordinal));

    // The issued token really is the approved platform profile — decoded, not trusted from the DTO.
    AssertPlatformTokenProfile(body.AccessToken, identityId, body.AuthenticationSessionId);

    // ...and the REAL bearer pipeline (JwtBearer -> StrictAccessTokenValidator -> platform plane guard) accepts it.
    var cookies = CookieJar(response);
    var logout = await SendAsync(HttpMethod.Post, $"{Prefix}/logout", cookies, body.AccessToken);
    Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
  }

  // ---- M2 : positive HTTP refresh rotation ----

  [Fact]
  public async Task Platform_refresh_rotates_the_continuation_and_denies_the_previous_token()
  {
    var (email, identityId) = await SeedEligibleOperatorAsync();
    var login = await LoginAsync(email);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var loginBody = await ReadAuthenticatedAsync(login);
    var firstCookies = CookieJar(login);

    var refresh = await SendAsync(HttpMethod.Post, $"{Prefix}/refresh", firstCookies);

    Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    var refreshed = await ReadAuthenticatedAsync(refresh);
    Assert.Equal(loginBody.AuthenticationSessionId, refreshed.AuthenticationSessionId); // same session, rotated token
    var rotatedCookies = CookieJar(refresh);
    Assert.NotEqual(
      firstCookies[PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName],
      rotatedCookies[PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName]);

    // The refreshed access token carries the same approved platform profile (not assumed from the login proof).
    AssertPlatformTokenProfile(refreshed.AccessToken, identityId, refreshed.AuthenticationSessionId);

    // Replaying the consumed refresh cookie is denied (one-time use).
    var replay = await SendAsync(HttpMethod.Post, $"{Prefix}/refresh", firstCookies);
    Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    await AssertProblemCodeAsync(replay, "authentication.refresh_failed");
  }

  // ---- M3 : positive HTTP logout ----

  [Fact]
  public async Task Platform_logout_revokes_the_session_clears_cookies_and_denies_further_refresh()
  {
    var (email, _) = await SeedEligibleOperatorAsync();
    var login = await LoginAsync(email);
    var body = await ReadAuthenticatedAsync(login);
    var cookies = CookieJar(login);
    var tenantSessionsBefore = await ScalarAsync("SELECT COUNT(*) FROM [platform].[AuthenticationSessions]");

    var logout = await SendAsync(HttpMethod.Post, $"{Prefix}/logout", cookies, body.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    // Both cookies are cleared on the platform path.
    var cleared = SetCookieHeaders(logout);
    Assert.Contains(cleared, header => header.StartsWith(PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName + "=;", StringComparison.Ordinal));
    Assert.Contains(cleared, header => header.StartsWith(AuthenticationCsrfService.CookieName + "=;", StringComparison.Ordinal));

    // Persisted effect: revoked with UserLogout.
    Assert.Equal("Revoked", Convert.ToString(await ScalarAsync(
      $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {body.AuthenticationSessionId}"), CultureInfo.InvariantCulture));
    Assert.Equal("UserLogout", Convert.ToString(await ScalarAsync(
      $"SELECT [RevocationReason] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {body.AuthenticationSessionId}"), CultureInfo.InvariantCulture));

    // Refresh continuation is denied after logout.
    var refresh = await SendAsync(HttpMethod.Post, $"{Prefix}/refresh", cookies);
    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

    // Platform-store only: the tenant session table is untouched.
    Assert.Equal(
      Convert.ToInt32(tenantSessionsBefore, CultureInfo.InvariantCulture),
      Convert.ToInt32(await ScalarAsync("SELECT COUNT(*) FROM [platform].[AuthenticationSessions]"), CultureInfo.InvariantCulture));
  }

  // ---- M5 : every ordinary ineligibility collapses to the same external failure ----

  [Fact]
  public async Task Every_ordinary_authority_failure_returns_the_same_generic_401()
  {
    var eligible = (await SeedEligibleOperatorAsync()).Email;
    var cases = new (string Label, string Email, string Password)[]
    {
      ("bad credentials", eligible, "not-the-password"),
      ("unknown login", $"nobody-{Guid.NewGuid():N}@example.test", Password),
      ("no platform principal", (await SeedEligibleOperatorAsync(withPrincipal: false)).Email, Password),
      ("disabled principal", (await SeedEligibleOperatorAsync(principalActive: false)).Email, Password),
      ("zero platform permissions", (await SeedEligibleOperatorAsync(grantPermission: false)).Email, Password),
      ("ineligible account", (await SeedEligibleOperatorAsync(accountEligible: false)).Email, Password)
    };

    foreach (var (label, email, password) in cases)
    {
      var response = await LoginAsync(email, password);

      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
      Assert.Equal("authentication.failed", document.RootElement.GetProperty("code").GetString());
      Assert.Equal("authentication.failed", document.RootElement.GetProperty("title").GetString());
      // No authority-state detail may distinguish the cases externally.
      Assert.False(document.RootElement.TryGetProperty("detail", out _), $"{label} leaked a detail field.");
      Assert.Empty(SetCookieHeaders(response));
    }
  }

  // ---- Cross-plane refresh isolation over HTTP ----

  [Fact]
  public async Task A_platform_refresh_cookie_presented_under_the_tenant_cookie_name_is_refused()
  {
    // Cookie names/paths differ by plane; presenting real platform refresh material under the tenant refresh
    // cookie name (and vice versa) must never authenticate a platform refresh.
    var (email, _) = await SeedEligibleOperatorAsync();
    var login = await LoginAsync(email);
    var cookies = CookieJar(login);
    var platformRefresh = cookies[PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName];
    var csrf = cookies[AuthenticationCsrfService.CookieName];

    // Only the TENANT-named refresh cookie is presented to the platform refresh route -> no platform cookie found.
    var mislabelled = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      [AuthenticationEndpointRouteBuilderExtensions.RefreshCookieName] = platformRefresh,
      [AuthenticationCsrfService.CookieName] = csrf
    };
    var response = await SendAsync(HttpMethod.Post, $"{Prefix}/refresh", mislabelled);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await AssertProblemCodeAsync(response, "authentication.request_rejected");

    // And the other direction: a foreign (non-platform-store) refresh token placed in the PLATFORM cookie is
    // refused too — the CSRF payload is bound to the real refresh token's public id, so it cannot be replayed.
    var foreign = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      [PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName] =
        $"{Guid.NewGuid():N}{Guid.NewGuid():N}.{Guid.NewGuid():N}{Guid.NewGuid():N}",
      [AuthenticationCsrfService.CookieName] = csrf
    };
    var foreignResponse = await SendAsync(HttpMethod.Post, $"{Prefix}/refresh", foreign);

    Assert.Equal(HttpStatusCode.Forbidden, foreignResponse.StatusCode);
    await AssertProblemCodeAsync(foreignResponse, "authentication.request_rejected");
  }

  // ---- Seeding ----

  private HttpClient Client => host.Client;

  private WebApplication App => host.Application;

  // Seeds a real identity + credentialed account + platform authority. Toggles produce the ordinary
  // ineligibility variants used by the external-failure matrix.
  private async Task<(string Email, long IdentityId)> SeedEligibleOperatorAsync(
    bool accountEligible = true, bool withPrincipal = true, bool principalActive = true, bool grantPermission = true)
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
    var email = $"operator-{Guid.NewGuid():N}@example.test";

    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    await context.SaveChangesAsync();

    var account = AuthenticationAccount.CreatePending(identity.Id, LoginEmail.Create(email).Value);
    if (accountEligible)
    {
      Assert.True(account.CompleteInitialSetup(hashing.HashPassword(Password), Guid.NewGuid(), Now).IsSuccess);
    }

    context.AuthenticationAccounts.Add(account);
    await context.SaveChangesAsync();

    if (withPrincipal)
    {
      var principal = PlatformSupportPrincipal.Register(identity.Id).Value;
      context.PlatformSupportPrincipals.Add(principal);
      await context.SaveChangesAsync();

      if (grantPermission)
      {
        Assert.True(new PlatformPermissionCatalog().TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var definition));
        Assert.True(principal.GrantPermission(definition, "seed", Now).IsSuccess);
        await context.SaveChangesAsync();
      }

      if (!principalActive)
      {
        Assert.True(principal.Disable("seed", Now).IsSuccess);
        await context.SaveChangesAsync();
      }
    }

    return (email, identity.Id);
  }

  // Decodes the issued JWT and asserts the exact approved platform profile (ADR-015 / DEC-TEN-0022).
  private static void AssertPlatformTokenProfile(string accessToken, long identityId, long sessionId)
  {
    var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    var planes = token.Claims.Where(claim => claim.Type == JwtClaimTypes.SecurityPlane).ToArray();
    Assert.Single(planes);
    Assert.Equal(SecurityPlane.Platform, planes[0].Value);
    Assert.Equal(identityId.ToString(CultureInfo.InvariantCulture), Single(token, JwtClaimTypes.IdentityId));
    Assert.Equal(sessionId.ToString(CultureInfo.InvariantCulture), Single(token, JwtClaimTypes.SessionId));
    Assert.Equal(AuthenticationClientId.V1Web, Single(token, JwtClaimTypes.ClientId));
    Assert.False(string.IsNullOrWhiteSpace(Single(token, JwtClaimTypes.SecurityVersion)));
    Assert.Contains(token.Claims, claim =>
      claim.Type == JwtClaimTypes.Permission && claim.Value == PlatformPermissionNames.AdministerPlatformSupport);

    foreach (var forbidden in new[]
      { JwtClaimTypes.TenantId, JwtClaimTypes.TenantUserId, JwtClaimTypes.CompanyId, JwtClaimTypes.Role, "principal_id" })
    {
      Assert.DoesNotContain(token.Claims, claim => claim.Type == forbidden);
    }
  }

  private static string Single(JwtSecurityToken token, string claimType) =>
    token.Claims.Single(claim => claim.Type == claimType).Value;

  private Task<HttpResponseMessage> LoginAsync(string loginEmail, string? password = null)
  {
    var payload = JsonSerializer.Serialize(new { loginEmail, password = password ?? Password });
    var request = new HttpRequestMessage(HttpMethod.Post, $"{Prefix}/login")
    {
      Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Origin", Origin);
    return Client.SendAsync(request);
  }

  // Sends a request presenting explicitly-constructed cookies plus the matching double-submit CSRF header.
  private Task<HttpResponseMessage> SendAsync(
    HttpMethod method, string path, Dictionary<string, string> cookies, string? bearer = null)
  {
    var request = new HttpRequestMessage(method, path);
    request.Headers.Add("Origin", Origin);
    request.Headers.Add("Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
    if (cookies.TryGetValue(AuthenticationCsrfService.CookieName, out var csrf))
    {
      request.Headers.Add(AuthenticationCsrfService.HeaderName, csrf);
    }

    if (bearer is not null)
    {
      request.Headers.Authorization = new("Bearer", bearer);
    }

    return Client.SendAsync(request);
  }

  private static string[] SetCookieHeaders(HttpResponseMessage response) =>
    response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

  private static Dictionary<string, string> CookieJar(HttpResponseMessage response)
  {
    var jar = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var header in SetCookieHeaders(response))
    {
      var pair = header.Split(';', 2)[0];
      var separator = pair.IndexOf('=', StringComparison.Ordinal);
      if (separator > 0)
      {
        jar[pair[..separator]] = pair[(separator + 1)..];
      }
    }

    return jar;
  }

  private static async Task<PlatformAuthenticatedResponse> ReadAuthenticatedAsync(HttpResponseMessage response)
  {
    var body = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<PlatformAuthenticatedResponse>(body, JsonOptions)
      ?? throw new InvalidOperationException($"Unexpected authentication response: {body}");
  }

  private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
  {
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
  }

  private Task<object?> ScalarAsync(string sql) => host.ScalarAsync(sql);
}

// One real host + one real database for the whole E2E collection. Building the in-memory host (JWT signing key
// + DataProtection ring) repeatedly was observed to flake under contention, so it is started once and shared;
// each test seeds its own identity/email, so the shared host stays test-independent.
public sealed class PlatformSupportAuthenticationEndToEndHost : IAsyncLifetime
{
  public const string Origin = "https://localhost:4200";
  public const string Password = "Sup3r-Secret-Platform-Pass!";
  private const string Issuer = "https://platform-support-e2e.tests";
  private const string Audience = "platform-support-e2e-tests";

  private WebApplication? application;
  private HttpClient? client;
  private string connectionString = string.Empty;

  public WebApplication Application => application ?? throw new InvalidOperationException("The test host has not started.");

  public HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  public async Task InitializeAsync()
  {
    var databaseName = $"SSAS_ERP_FP003_E2E_{Guid.NewGuid():N}";
    var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
    // TEST-ONLY: the command timeout is carried on the test's own connection string (never production config).
    // This host creates and migrates its database while other suites are also hammering the local SQL Server, so
    // the 30s default can expire during migration; 120s absorbs that contention and still fails a genuine hang.
    connectionString = new SqlConnectionStringBuilder(configured)
    {
      InitialCatalog = databaseName,
      CommandTimeout = 120
    }.ConnectionString;

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:Platform"] = connectionString,
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30",
      ["AuthenticationTransport:AllowedOrigins:0"] = Origin,
      ["AuthenticationTransport:RateLimitHmacSecret"] = "platform-support-e2e-rate-limit-secret-value-0123456789"
    });
    builder.Services
      .AddPlatformInfrastructure(builder.Configuration)
      .AddPlatformRequestContext()
      .AddPlatformModule()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostAuthenticationTransport(builder.Configuration, builder.Environment)
      .AddHostProblemDetails();

    application = builder.Build();
    await using (var scope = application.Services.CreateAsyncScope())
    {
      await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
    }

    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformSupportAuthenticationEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
    client.BaseAddress = new Uri("https://localhost");
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null)
    {
      await using (var scope = application.Services.CreateAsyncScope())
      {
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.EnsureDeletedAsync();
      }

      await application.DisposeAsync();
    }
  }

  public async Task<object?> ScalarAsync(string sql)
  {
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return await command.ExecuteScalarAsync();
  }
}

// The E2E host owns a real database and the singleton rate limiter, so the collection runs alone.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlatformSupportAuthenticationEndToEndGroup : ICollectionFixture<PlatformSupportAuthenticationEndToEndHost>
{
  public const string Name = "Platform support authentication E2E";
}
