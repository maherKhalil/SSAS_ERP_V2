using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

// Phase 4B (DEC-TEN-0023) — end-to-end proof that the platform-support LOGOUT route is platform-plane only. A REAL
// signed JWT flows through JWT bearer authentication -> StrictAccessTokenValidator -> the endpoint's inline plane
// guard. A tenant (or plane-less) token can never reach the revoke path; a platform token still needs a matching
// CSRF-bound refresh cookie. Nothing is mocked at the authentication boundary.
public sealed class PlatformSupportAuthenticationLogoutPipelineTests : IAsyncLifetime
{
  private const string Issuer = "https://platform-support-logout.tests";
  private const string Audience = "platform-support-logout-tests";
  private const string LogoutPath = "/api/platform/support/auth/logout";
  private WebApplication? application;
  private HttpClient? client;

  [Fact]
  public async Task Unauthenticated_logout_is_unauthorized()
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, LogoutPath);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Tenant_plane_token_cannot_invoke_platform_logout()
  {
    // A fully valid tenant token authenticates but carries no security_plane=platform — the inline plane guard
    // rejects it before any platform session is touched. This is the cross-plane isolation invariant.
    using var request = new HttpRequestMessage(HttpMethod.Post, LogoutPath);
    request.Headers.Authorization = new("Bearer", SignToken(TenantClaims()));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await AssertProblemCodeAsync(response, "authentication.request_rejected");
  }

  [Fact]
  public async Task Platform_token_without_the_matching_csrf_cookie_is_rejected()
  {
    // A valid platform token passes the plane guard but still must present the platform refresh cookie bound to a
    // CSRF header for the same session — logout is a state-changing POST protected by double-submit CSRF.
    using var request = new HttpRequestMessage(HttpMethod.Post, LogoutPath);
    request.Headers.Authorization = new("Bearer", SignToken(PlatformClaims()));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await AssertProblemCodeAsync(response, "authentication.request_rejected");
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30"
    });
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    // Transport reject-path dependencies only: the revoke handler is never resolved on a rejected request, so no
    // platform persistence is wired here — these tests exercise the authentication/plane/CSRF boundary in isolation.
    builder.Services.AddSingleton<IAuthenticationRequestSecurity>(new AcceptingRequestSecurity());
    builder.Services.AddSingleton<IAuthenticationEndpointRateLimiter>(new AllowingRateLimiter());
    builder.Services.AddDataProtection();
    builder.Services.AddScoped<AuthenticationCsrfService>();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformSupportAuthenticationEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private static IEnumerable<Claim> BaseClaims(DateTimeOffset now) =>
  [
    new Claim(JwtClaimTypes.Subject, "platform-operator"),
    new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
    new Claim("iat", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
    new Claim(JwtClaimTypes.IdentityId, "11"),
    new Claim(JwtClaimTypes.SessionId, "33"),
    new Claim(JwtClaimTypes.ClientId, AuthenticationClientId.V1Web),
    new Claim(JwtClaimTypes.SecurityVersion, "1")
  ];

  private static List<Claim> PlatformClaims()
  {
    var claims = BaseClaims(DateTimeOffset.UtcNow).ToList();
    claims.Add(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform));
    claims.Add(new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.AdministerPlatformSupport));
    return claims;
  }

  private static List<Claim> TenantClaims()
  {
    var claims = BaseClaims(DateTimeOffset.UtcNow).ToList();
    claims.Add(new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")));
    claims.Add(new Claim(JwtClaimTypes.TenantUserId, "22"));
    claims.Add(new Claim(JwtClaimTypes.Permission, "test.permission"));
    return claims;
  }

  private string SignToken(IEnumerable<Claim> claims)
  {
    var key = application?.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey
      ?? throw new InvalidOperationException("The test application is unavailable.");
    var now = DateTimeOffset.UtcNow;
    var token = new JwtSecurityToken(
      issuer: Issuer,
      audience: Audience,
      claims: claims,
      notBefore: now.AddMinutes(-1).UtcDateTime,
      expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
  {
    using var document = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
  }

  private sealed class AcceptingRequestSecurity : IAuthenticationRequestSecurity
  {
    public bool IsAccepted(HttpContext context, bool requireJson) => true;
  }

  private sealed class AllowingRateLimiter : IAuthenticationEndpointRateLimiter
  {
    public ValueTask<AuthenticationRateLimitResult> AcquireAsync(
      AuthenticationEndpointKind endpoint, HttpContext context, string partitionMaterial,
      CancellationToken cancellationToken = default) =>
      ValueTask.FromResult(new AuthenticationRateLimitResult(true, TimeSpan.Zero));
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }
}
