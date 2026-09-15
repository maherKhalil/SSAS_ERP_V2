using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Subscriptions.TenantSubscriptions;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Infrastructure.RequestContext;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Subscriptions;

[CollectionDefinition("TenantSubscriptions API endpoints", DisableParallelization = true)]
public sealed class TenantSubscriptionsApiEndpointGroup {}

[Collection("TenantSubscriptions API endpoints")]
public sealed class TenantSubscriptionsEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://plans.tests";
  private const string Audience = "plans-tests";
  private static readonly Guid TenantId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
  private static readonly Guid KnownPlanId = Guid.Parse("11111111-2222-3333-4444-555555555555");
  private static readonly Guid SubId = Guid.Parse("22222222-3333-4444-5555-666666666666");

  private WebApplication? application;
  private HttpClient? client;
  private StubTenantSubscriptionRepository repository = new();
  private StubTenantSubscriptionQueries queries = new();
  private StubPlatformUnitOfWork unitOfWork = new();
  private StubTenantEntitlementCache entitlementCache = new();

  [Fact]
  public async Task GetTenantSubscriptions_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get($"/api/platform/tenants/{TenantId}/subscriptions", null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetTenantSubscriptions_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"/api/platform/tenants/{TenantId}/subscriptions", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task GetCurrentTenantSubscription_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"/api/platform/tenants/{TenantId}/subscriptions/current", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task GetAllSubscriptions_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"/api/platform/subscriptions", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task AppendTenantSubscription_authorized_returns_201()
  {
    repository.PlanExists = true;
    var body = $"{{\"subscriptionPlanId\":\"{KnownPlanId}\",\"effectiveFromUtc\":\"2026-01-01T00:00:00Z\",\"termKind\":\"Perpetual\",\"termStartUtc\":\"2026-01-01T00:00:00Z\",\"billingCurrencyCode\":\"USD\"}}";
    var response = await Client.SendAsync(Post($"/api/platform/tenants/{TenantId}/subscriptions", body, AdministerToken()));
    
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

    repository = new StubTenantSubscriptionRepository();
    queries = new StubTenantSubscriptionQueries();
    unitOfWork = new StubPlatformUnitOfWork();
    entitlementCache = new StubTenantEntitlementCache();

    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();

    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    
    builder.Services.AddSingleton<ITenantSubscriptionRepository>(repository);
    builder.Services.AddSingleton<ITenantSubscriptionQueries>(queries);
    builder.Services.AddSingleton<IPlatformUnitOfWork>(unitOfWork);
    builder.Services.AddSingleton<ITenantEntitlementCache>(entitlementCache);

    builder.Services.AddScoped<GetTenantSubscriptionsQueryHandler>();
    builder.Services.AddScoped<GetCurrentTenantSubscriptionQueryHandler>();
    builder.Services.AddScoped<GetAllSubscriptionsQueryHandler>();
    builder.Services.AddScoped<AppendTenantSubscriptionCommandHandler>();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformTenantSubscriptionsEndpoints();

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
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.AdministerSubscriptions));

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

  private sealed class StubTenantSubscriptionRepository : ITenantSubscriptionRepository
  {
    public bool PlanExists { get; set; }
    public bool AddCalled { get; private set; }

    public Task<DateTimeOffset?> GreatestEffectiveFromUtcAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult<DateTimeOffset?>(null);

    public Task<bool> PlanExistsAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default)
      => Task.FromResult(PlanExists);

    public Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default)
    {
      AddCalled = true;
      return Task.CompletedTask;
    }
  }

  private sealed class StubTenantSubscriptionQueries : ITenantSubscriptionQueries
  {
    public Task<Result<IEnumerable<TenantSubscriptionDto>>> GetTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success<IEnumerable<TenantSubscriptionDto>>([]));

    public Task<Result<TenantSubscriptionDto>> GetCurrentTenantSubscriptionAsync(Guid tenantId, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success(new TenantSubscriptionDto()));

    public Task<Result<IEnumerable<TenantSubscriptionDto>>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success<IEnumerable<TenantSubscriptionDto>>([]));
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

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => GetEligibilityAsync(tenantId, cancellationToken);
  }
}
