using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

public sealed class AuthorizationPipelineTests : IAsyncLifetime
{
  private const string Issuer = "https://authorization.tests";
  private const string Audience = "authorization-tests";
  private static readonly Guid TenantId = Guid.Parse("64fbfdcc-c6e3-4626-ad14-ed4f7aa156e1");
  private WebApplication? application;
  private HttpClient? client;
  private MutableTenantEligibility? tenantEligibility;

  [Fact]
  public async Task Unauthenticated_permission_request_returns_401_with_a_correlation_id()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/test/permission");
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "authorization-401");

    var response = await Client.SendAsync(request);

    await AssertAuthorizationFailureAsync(response, HttpStatusCode.Unauthorized, "authorization-401");
    Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
    Assert.Equal("no-cache", response.Headers.Pragma.ToString());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  [Fact]
  public async Task Authenticated_user_without_permission_returns_403_with_a_correlation_id()
  {
    using var request = CreateAuthorizedRequest("/test/permission", new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "authorization-403");

    var response = await Client.SendAsync(request);

    await AssertAuthorizationFailureAsync(response, HttpStatusCode.Forbidden, "authorization-403");
  }

  [Fact]
  public async Task Authenticated_user_with_matching_permission_and_tenant_is_authorized()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Role_policy_uses_the_validated_role_claim()
  {
    using var request = CreateAuthorizedRequest(
      "/test/role",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Role, "test.role"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Missing_tenant_claim_is_rejected_as_an_invalid_access_token()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Theory]
  [InlineData(TenantStatus.Provisioning)]
  [InlineData(TenantStatus.Suspended)]
  [InlineData(TenantStatus.Archived)]
  [InlineData(null)]
  public async Task Non_active_or_missing_tenant_is_rejected(TenantStatus? status)
  {
    TenantEligibility.Status = status;
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Already_issued_token_is_immediately_rejected_after_tenant_suspension()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));
    TenantEligibility.Status = TenantStatus.Suspended;

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Role_and_permission_authorization_share_one_live_tenant_lookup_per_request()
  {
    TenantEligibility.Calls = 0;
    using var request = CreateAuthorizedRequest(
      "/test/combined",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"),
      new Claim(JwtClaimTypes.Role, "test.role"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, TenantEligibility.Calls);
  }

  [Fact]
  public async Task Suspended_tenant_session_can_still_use_the_separate_logout_policy()
  {
    TenantEligibility.Status = TenantStatus.Suspended;
    using var request = CreateAuthorizedRequest(
      "/test/logout",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      EnvironmentName = Environments.Development
    });
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
    tenantEligibility = new MutableTenantEligibility();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(tenantEligibility);
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapGet("/test/permission", () => Results.Ok())
      .RequireAuthorization(PermissionAuthorizationDefaults.CreatePolicyName("test.permission"));
    application.MapGet("/test/role", () => Results.Ok())
      .RequireAuthorization(RoleAuthorizationDefaults.CreatePolicyName("test.role"));
    application.MapGet("/test/combined", () => Results.Ok())
      .RequireAuthorization(
        PermissionAuthorizationDefaults.CreatePolicyName("test.permission"),
        RoleAuthorizationDefaults.CreatePolicyName("test.role"));
    application.MapGet("/test/logout", () => Results.NoContent()).RequireAuthorization();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    if (client is not null)
    {
      client.Dispose();
    }

    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private MutableTenantEligibility TenantEligibility => tenantEligibility ??
    throw new InvalidOperationException("The tenant eligibility service has not started.");

  private HttpRequestMessage CreateAuthorizedRequest(string path, params Claim[] claims)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", CreateToken(claims));
    return request;
  }

  private string CreateToken(IEnumerable<Claim> claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(
      keyProvider.Snapshot.ActiveSigningKey,
      SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;
    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "test-user"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new Claim(JwtClaimTypes.IdentityId, "1"),
      new Claim(JwtClaimTypes.TenantUserId, "2"),
      new Claim(JwtClaimTypes.SessionId, "3"),
      new Claim(JwtClaimTypes.ClientId, "ssas-erp-web"),
      new Claim(JwtClaimTypes.SecurityVersion, "1")
    };
    var token = new JwtSecurityToken(
      issuer: Issuer,
      audience: Audience,
      claims: requiredClaims.Concat(claims),
      notBefore: now.AddMinutes(-1).UtcDateTime,
      expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private sealed class MutableTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public TenantStatus? Status { get; set; } = TenantStatus.Active;
    public int Calls { get; set; }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Read(tenantId));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);

    private TenantAuthenticationEligibilityResult Read(Guid tenantId)
    {
      Calls++;
      return TenantAuthenticationEligibilityResult.FromStatus(tenantId, Status);
    }
  }

  private static async Task AssertAuthorizationFailureAsync(
    HttpResponseMessage response,
    HttpStatusCode expectedStatusCode,
    string expectedCorrelationId)
  {
    Assert.Equal(expectedStatusCode, response.StatusCode);
    Assert.Equal(expectedCorrelationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCorrelationId, document.RootElement.GetProperty("correlationId").GetString());
    Assert.Equal(
      expectedStatusCode == HttpStatusCode.Unauthorized
        ? "platform.authentication.errors.authentication_failed"
        : "platform.authentication.errors.request_rejected",
      document.RootElement.GetProperty("resourceKey").GetString());
  }
}
