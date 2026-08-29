using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Roles;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.IdentityAccess;

// Proves the GET /api/platform/roles proof endpoint end-to-end through the REAL Host
// authentication + authorization pipeline and the shared transport primitives. The database is
// replaced by a recording read-service stub; the real ListRolesQueryHandler still enforces the
// trusted tenant context and paging bounds, and the endpoint accepts no caller-supplied TenantId.
public sealed class RolesEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://identity-access.tests";
  private const string Audience = "identity-access-tests";
  private static readonly Guid TenantId = Guid.Parse("2f0b6a9e-2b3c-4a2f-9c1e-9a0b7d6e5f40");
  private WebApplication? application;
  private HttpClient? client;
  private RecordingRoleReadService roleReadService = new();

  [Fact]
  public async Task Unauthenticated_request_returns_401()
  {
    var response = await Client.GetAsync("/api/platform/roles");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(roleReadService.Called);
  }

  [Fact]
  public async Task Authenticated_without_view_permission_returns_403()
  {
    using var request = Authorized(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(roleReadService.Called);
  }

  [Fact]
  public async Task Missing_trusted_tenant_claim_is_rejected_as_an_invalid_token()
  {
    using var request = Authorized(new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewRoles));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(roleReadService.Called);
  }

  [Fact]
  public async Task Authorized_request_returns_200_with_the_tenant_scoped_projection()
  {
    using var request = Authorized(
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewRoles));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(roleReadService.Called);
    var page = await response.Content.ReadFromJsonAsync<RolePageResponse>();
    Assert.NotNull(page);
    var role = Assert.Single(page!.Items);
    Assert.Equal("Tenant Administrator", role.Name);
    Assert.Equal("Custom", role.RoleType);
    Assert.Equal("Active", role.Status);
    Assert.Equal("AQIDBAUGBwg=", role.RowVersion); // canonical padded Base64 via the shared codec
    AssertSecurityHeaders(response);
  }

  // ================================================================================================
  // THE PERMISSION CATALOGUE (T-203) — THE ONE ROW OF THE CAPABILITY GAP THAT NEEDED NO DECISION.
  // ================================================================================================
  //
  // An audit of the 67 documented-but-unrouted rows put 41 behind five owner decisions, 15 behind an
  // accepted deferral and 10 down to capability that already exists under another path. This was the
  // remainder: a read of a static catalogue, whose handler was written and registered and whose permission
  // was catalogued, waiting only for six lines of transport.
  [Fact]
  public async Task The_catalogue_requires_its_own_permission()
  {
    using var request = Authorized(
      "/api/platform/permissions",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewRoles));

    var response = await Client.SendAsync(request);

    // `ViewRoles` is the NEIGHBOURING permission and the one most likely to be reached for by mistake:
    // roles and permissions sit in the same document and the same route group. It is not this route's.
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task The_catalogue_lists_tenant_assignable_permissions_only()
  {
    using var request = Authorized(
      "/api/platform/permissions",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewPermissions));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var catalogue = await response.Content.ReadFromJsonAsync<PermissionCatalogResponse>();
    Assert.NotNull(catalogue);
    Assert.NotEmpty(catalogue!.Items);

    // ⚠ THE ASSERTION THAT MATTERS. `ADR-015`'s PlatformSupport-scoped permissions are never assignable by
    // a tenant, and listing one to a tenant administrator would advertise an authority they cannot be
    // granted. The handler filters; this proves the filter survives the transport.
    Assert.All(catalogue.Items, item => Assert.Equal("Tenant", item.Scope));

    // The scope travels as a STRING. A numeric enum would let a reordering silently change what an
    // existing value means to a client that has already shipped.
    Assert.All(catalogue.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task The_catalogue_accepts_no_query_parameters()
  {
    using var request = Authorized(
      "/api/platform/permissions?pageNumber=1",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewPermissions));

    var response = await Client.SendAsync(request);

    // Not paged and not filtered. Accepting and ignoring a parameter would be a promise the route does not
    // keep, and a caller who paged it would believe they had seen everything.
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Invalid_paging_returns_400_request_invalid()
  {
    using var request = Authorized(
      "/api/platform/roles?pageNumber=1&pageSize=0",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewRoles));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Unknown_query_parameter_returns_400_request_invalid()
  {
    using var request = Authorized(
      "/api/platform/roles?unexpected=1",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewRoles));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.False(roleReadService.Called);
  }

  [Fact]
  public async Task A_caller_supplied_tenant_id_query_is_rejected_as_unknown()
  {
    using var request = Authorized(
      $"/api/platform/roles?tenantId={Guid.NewGuid()}",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewRoles));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.False(roleReadService.Called);
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
    roleReadService = new RecordingRoleReadService();
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddSingleton<IRoleReadService>(roleReadService);
    builder.Services.AddScoped<ListRolesQueryHandler>();

    // ---- ⚠ ADDED WITH THE PERMISSION-CATALOGUE ROUTE (T-203), AND ITS ABSENCE BROKE EVERY TEST ABOVE.
    //
    // This host maps the identity-access group, so routing ONE more endpoint made its dependencies
    // construction-time dependencies of the whole host — and DI validation failed all four existing tests
    // with a message about a handler none of them calls.
    //
    // **That is the good failure mode**: a test host that omits a registration proves the production wiring
    // only by accident, and this one said so loudly the moment production gained a route.
    builder.Services.AddSingleton<PlatformPermissionCatalog>();
    builder.Services.AddSingleton<IPermissionCatalog, ComposedPermissionCatalog>();
    builder.Services.AddScoped<ListPermissionCatalogQueryHandler>();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformIdentityAccessEndpoints();

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

  private HttpRequestMessage Authorized(params Claim[] claims) => Authorized("/api/platform/roles", claims);

  private HttpRequestMessage Authorized(string path, params Claim[] claims)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", CreateToken(claims));
    return request;
  }

  private string CreateToken(IEnumerable<Claim> claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
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

  private static void AssertSecurityHeaders(HttpResponseMessage response)
  {
    Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
    Assert.Equal("no-cache", response.Headers.Pragma.ToString());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedCode)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
  }

  private sealed class RecordingRoleReadService : IRoleReadService
  {
    public bool Called { get; private set; }

    public Task<RoleDto?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult<RoleDto?>(null);

    public Task<PagedResult<RoleDto>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
      Called = true;
      var role = new RoleDto(7, "Tenant Administrator", "Tenant role", RoleType.Custom, RoleStatus.Active,
        ["Platform.Roles.View"], [1, 2, 3, 4, 5, 6, 7, 8]);
      return Task.FromResult(new PagedResult<RoleDto>([role], pageNumber, pageSize, 1));
    }
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }
}
