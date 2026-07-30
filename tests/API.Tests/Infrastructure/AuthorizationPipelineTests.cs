using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

public sealed class AuthorizationPipelineTests : IAsyncLifetime
{
  private const string SigningKey = "TestSigningKey-ForAuthorizationPipeline-NotASecret";
  private const string Issuer = "https://authorization.tests";
  private const string Audience = "authorization-tests";
  private static readonly Guid TenantId = Guid.Parse("64fbfdcc-c6e3-4626-ad14-ed4f7aa156e1");
  private WebApplication? application;
  private HttpClient? client;

  [Fact]
  public async Task Unauthenticated_permission_request_returns_401_with_a_correlation_id()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/test/permission");
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "authorization-401");

    var response = await Client.SendAsync(request);

    await AssertAuthorizationFailureAsync(response, HttpStatusCode.Unauthorized, "authorization-401");
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
  public async Task Missing_tenant_claim_is_forbidden_even_when_the_permission_claim_matches()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
      ["Jwt:SigningKey"] = SigningKey,
      ["Jwt:ClockSkewSeconds"] = "0"
    });
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapGet("/test/permission", () => Results.Ok())
      .RequireAuthorization(PermissionAuthorizationDefaults.CreatePolicyName("test.permission"));
    application.MapGet("/test/role", () => Results.Ok())
      .RequireAuthorization(RoleAuthorizationDefaults.CreatePolicyName("test.role"));

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

  private static HttpRequestMessage CreateAuthorizedRequest(string path, params Claim[] claims)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", CreateToken(claims));
    return request;
  }

  private static string CreateToken(IEnumerable<Claim> claims)
  {
    var credentials = new SigningCredentials(
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
      SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
      issuer: Issuer,
      audience: Audience,
      claims: claims.Append(new Claim(JwtRegisteredClaimNames.Sub, "test-user")),
      notBefore: DateTime.UtcNow.AddMinutes(-1),
      expires: DateTime.UtcNow.AddMinutes(5),
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
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
  }
}
