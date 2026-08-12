using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
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
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

// Phase 4A end-to-end authorization proof (closes L3): a REAL signed platform JWT flows through JWT bearer
// authentication -> StrictAccessTokenValidator -> the dynamic PlatformPermission policy ->
// PlatformPermissionAuthorizationHandler -> endpoint. Nothing is mocked. Proves platform authorization succeeds
// only for a validated platform token holding the exact PlatformSupport permission, and that neither plane can
// cross into the other's policy.
public sealed class PlatformAuthorizationPipelineTests : IAsyncLifetime
{
  private const string Issuer = "https://platform-authorization.tests";
  private const string Audience = "platform-authorization-tests";
  private const string Administer = PlatformPermissionNames.AdministerPlatformSupport; // PlatformSupport scope
  private const string ViewTenants = PlatformPermissionNames.ViewTenants;              // PlatformSupport scope
  private const string ViewCompanies = PlatformPermissionNames.ViewCompanies;          // Tenant scope
  private const string UnknownPermission = "Platform.Support.DoesNotExist";
  private WebApplication? application;
  private HttpClient? client;

  // ---- STEP 24 / STEP 25 : platform token success + missing permission ----

  [Fact]
  public async Task Valid_platform_token_with_the_required_permission_is_authorized()
  {
    using var request = PlatformRequest("/platform-test/administer", permissions: [Administer]);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Valid_platform_token_without_the_required_permission_is_forbidden()
  {
    using var request = PlatformRequest("/platform-test/administer", permissions: [ViewTenants]);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // authenticated but unauthorized (403, not 401)
  }

  // ---- STEP 26 : real tenant token against a platform policy ----

  [Fact]
  public async Task Real_tenant_token_even_with_the_same_permission_text_cannot_satisfy_a_platform_policy()
  {
    // A valid tenant token that (illegally) carries the platform permission name still lacks security_plane=platform.
    using var request = TenantRequest("/platform-test/administer", permission: Administer);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- STEP 27 : real platform token against a tenant policy ----

  [Fact]
  public async Task Real_platform_token_cannot_satisfy_a_tenant_permission_policy()
  {
    using var request = PlatformRequest("/tenant-test/permission", permissions: [Administer, "test.permission"]);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // no validated tenant context
  }

  // ---- STEP 28 : mixed-plane token is rejected at authentication ----

  [Fact]
  public async Task Mixed_plane_token_is_rejected_at_authentication_before_authorization()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/platform-test/administer");
    // security_plane=platform + a forbidden tenant_id → StrictAccessTokenValidator fails the token.
    request.Headers.Authorization = new("Bearer", SignToken(
        PlatformClaims([Administer]).Append(new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")))));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---- STEP 29 : unknown catalog permission (validator does not catalog-check; handler does) ----

  [Fact]
  public async Task Platform_token_with_an_unknown_catalog_permission_is_forbidden()
  {
    using var request = PlatformRequest("/platform-test/unknown", permissions: [UnknownPermission]);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- STEP 30 : tenant-scoped permission name through a platform policy ----

  [Fact]
  public async Task Platform_token_carrying_a_tenant_scoped_permission_name_is_forbidden()
  {
    using var request = PlatformRequest("/platform-test/view-companies", permissions: [ViewCompanies]);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // ViewCompanies is PermissionScope.Tenant
  }

  [Fact]
  public async Task Unauthenticated_platform_request_is_unauthorized()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/platform-test/administer");

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
    // The tenant permission/role handlers are always resolved during authorization; give them a live-eligibility stub.
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapGet("/platform-test/administer", () => Results.Ok())
      .RequireAuthorization(PlatformPermissionAuthorizationDefaults.CreatePolicyName(Administer));
    application.MapGet("/platform-test/unknown", () => Results.Ok())
      .RequireAuthorization(PlatformPermissionAuthorizationDefaults.CreatePolicyName(UnknownPermission));
    application.MapGet("/platform-test/view-companies", () => Results.Ok())
      .RequireAuthorization(PlatformPermissionAuthorizationDefaults.CreatePolicyName(ViewCompanies));
    application.MapGet("/tenant-test/permission", () => Results.Ok())
      .RequireAuthorization(PermissionAuthorizationDefaults.CreatePolicyName("test.permission"));

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

  private HttpRequestMessage PlatformRequest(string path, string[] permissions)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", SignToken(PlatformClaims(permissions)));
    return request;
  }

  private HttpRequestMessage TenantRequest(string path, string permission)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", SignToken(TenantClaims(permission)));
    return request;
  }

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

  private static List<Claim> PlatformClaims(IEnumerable<string> permissions)
  {
    var claims = BaseClaims(DateTimeOffset.UtcNow).ToList();
    claims.Add(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform));
    claims.AddRange(permissions.Select(permission => new Claim(JwtClaimTypes.Permission, permission)));
    return claims;
  }

  private static List<Claim> TenantClaims(string permission)
  {
    var claims = BaseClaims(DateTimeOffset.UtcNow).ToList();
    claims.Add(new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")));
    claims.Add(new Claim(JwtClaimTypes.TenantUserId, "22"));
    claims.Add(new Claim(JwtClaimTypes.Permission, permission));
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

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }
}
