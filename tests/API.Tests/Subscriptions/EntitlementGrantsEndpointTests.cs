using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.Application.Subscriptions.EntitlementGrants;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.Infrastructure.RequestContext;
using SSAS.Platform.Application.Tenants;

namespace SSAS.API.Tests.Subscriptions;

public sealed class EntitlementGrantsApiEndpointGroup {}

[Collection(nameof(EntitlementGrantsApiEndpointGroup))]
public sealed class EntitlementGrantsEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://identity.example";
  private const string Audience = "ssas-erp";

  private WebApplication? application;
  private HttpClient? client;
  private StubTenantEntitlementGrantRepository repository = null!;
  private StubTenantEntitlementGrantQueries queries = null!;
  private StubPlatformUnitOfWork unitOfWork = null!;
  private StubTenantEntitlementReader reader = null!;
  private StubTenantEntitlementCache entitlementCache = null!;

  private static readonly Guid TenantId = Guid.Parse("A89A6DF0-3E6D-4E1A-A6DF-03E6D4E1A9D0");

  [Fact]
  public async Task GetTenantGrants_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"/api/platform/tenants/{TenantId}/grants", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task GrantEntitlement_authorized_returns_201()
  {
    var body = $"{{\"grantKind\":\"moduleGrant\",\"moduleKey\":\"Payroll\",\"effectiveFromUtc\":\"2026-01-01T00:00:00Z\"}}";
    var response = await Client.SendAsync(Post($"/api/platform/tenants/{TenantId}/grants", body, AdministerToken()));
    
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.True(repository.AddCalled);
    Assert.Equal(1, unitOfWork.SaveCount);
    Assert.True(entitlementCache.InvalidateCalled);
  }

  [Fact]
  public async Task RevokeEntitlement_authorized_returns_201()
  {
    var body = $"{{\"grantKind\":\"moduleGrant\",\"moduleKey\":\"Payroll\",\"effectiveFromUtc\":\"2026-01-01T00:00:00Z\"}}";
    var response = await Client.SendAsync(Post($"/api/platform/tenants/{TenantId}/grants/revoke", body, AdministerToken()));
    
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.True(repository.AddCalled);
    Assert.Equal(1, unitOfWork.SaveCount);
    Assert.True(entitlementCache.InvalidateCalled);
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

    repository = new StubTenantEntitlementGrantRepository();
    queries = new StubTenantEntitlementGrantQueries();
    unitOfWork = new StubPlatformUnitOfWork();
    entitlementCache = new StubTenantEntitlementCache();
    reader = new StubTenantEntitlementReader();

    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();

    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    
    builder.Services.AddSingleton<ITenantEntitlementGrantRepository>(repository);
    builder.Services.AddSingleton<ITenantEntitlementGrantQueries>(queries);
    builder.Services.AddSingleton<IPlatformUnitOfWork>(unitOfWork);
    builder.Services.AddSingleton<ITenantEntitlementCache>(entitlementCache);
    builder.Services.AddSingleton<ITenantEntitlementReader>(reader);

    builder.Services.AddScoped<GetTenantGrantsQueryHandler>();
    builder.Services.AddScoped<EntitlementGrantsCommandHandler>();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformEntitlementGrantsEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null) await application.DisposeAsync();
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("Test host has not started.");

  private static HttpRequestMessage Get(string path, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (token is not null) request.Headers.Authorization = new("Bearer", token);
    return request;
  }

  private static HttpRequestMessage Post(string path, string? body, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, path);
    if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    if (token is not null) request.Headers.Authorization = new("Bearer", token);
    return request;
  }

  private string ViewToken() => Token(
    new Claim(JwtClaimTypes.SecurityPlane, "platform"),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewSubscriptions));

  private string AdministerToken() => Token(
    new Claim(JwtClaimTypes.SecurityPlane, "platform"),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.AdministerEntitlementGrants));

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>()
      ?? throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;
    
    var requiredClaims = new List<Claim>
    {
      new Claim(JwtClaimTypes.Subject, "admin"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new Claim(JwtClaimTypes.IdentityId, "1"),
      new Claim(JwtClaimTypes.SessionId, "3"),
      new Claim(JwtClaimTypes.ClientId, "ssas-erp-web"),
      new Claim(JwtClaimTypes.SecurityVersion, "1")
    };
    
    var token = new JwtSecurityToken(
      issuer: Issuer, audience: Audience, claims: requiredClaims.Concat(claims),
      notBefore: now.AddMinutes(-1).UtcDateTime, expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: credentials);
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private sealed class StubTenantEntitlementGrantRepository : ITenantEntitlementGrantRepository
  {
    public bool AddCalled { get; private set; }

    public Task AddAsync(TenantEntitlementGrant grant, CancellationToken cancellationToken = default)
    {
      AddCalled = true;
      return Task.CompletedTask;
    }
  }

  private sealed class StubTenantEntitlementGrantQueries : ITenantEntitlementGrantQueries
  {
    public Task<Result<IReadOnlyList<TenantEntitlementGrantDto>>> GetTenantGrantsAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success<IReadOnlyList<TenantEntitlementGrantDto>>([]));
  }

  private sealed class StubPlatformUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class StubTenantEntitlementCache : ITenantEntitlementCache
  {
    public bool InvalidateCalled { get; private set; }
    public bool TryGet(Guid tenantId, out TenantEntitlementSnapshot snapshot) { snapshot = null!; return false; }
    public void Store(TenantEntitlementSnapshot snapshot) { }
    public void InvalidateTenant(Guid tenantId) { InvalidateCalled = true; }
    public void InvalidatePlan(Guid subscriptionPlanId) { }
  }

  private sealed class StubTenantEntitlementReader : ITenantEntitlementReader
  {
    public Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(new TenantEntitlementSnapshot(tenantId, Guid.NewGuid(), null, new HashSet<string>(), new Dictionary<string, long>(), new List<EntitlementGrantFact>()));
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => GetEligibilityAsync(tenantId, cancellationToken);
  }
}
