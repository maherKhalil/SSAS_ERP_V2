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
using SSAS.Platform.Application.Subscriptions.Plans;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Infrastructure.RequestContext;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Subscriptions;

// Plans API endpoint tests share one non-parallel collection so their in-memory hosts do not start
// concurrently, which was observed to flake under contention in the Companies test suite. Following
// the same pattern as CompanyApiEndpointGroup.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlanApiEndpointGroup
{
  public const string Name = "Plan API endpoints";
}

// Proves POST /api/platform/plans, PUT /api/platform/plans/{planId}, POST .../retire,
// PUT .../modules, PUT .../limits, PUT .../prices, GET /api/platform/plans,
// GET /api/platform/plans/{planId} end-to-end through the REAL Host authentication +
// authorization pipeline, the shared StrictRequestReader on a real body, the
// SubscriptionApiErrorMapper, and the real command/query handlers. The database is replaced
// by recording stubs; the handlers still validate value objects.
[Collection(PlanApiEndpointGroup.Name)]
public sealed class PlansEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://plans.tests";
  private const string Audience = "plans-tests";
  private const string RoutePrefix = "/api/platform/plans";
  private static readonly Guid TenantId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
  private static readonly Guid KnownPlanId = Guid.Parse("11111111-2222-3333-4444-555555555555");

  private WebApplication? application;
  private HttpClient? client;
  private StubPlanRepository repository = new();
  private StubPlanQueries planQueries = new();
  private StubPlatformUnitOfWork unitOfWork = new();

  // ==============================================================================================
  // GET /api/platform/plans — authorization
  // ==============================================================================================

  [Fact]
  public async Task GetPlans_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get(RoutePrefix, token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetPlans_without_view_permission_returns_403()
  {
    var response = await Client.SendAsync(Get(RoutePrefix,
      Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // GET /api/platform/plans — success
  // ==============================================================================================

  [Fact]
  public async Task GetPlans_authorized_returns_200_with_plan_list()
  {
    var response = await Client.SendAsync(Get(RoutePrefix, ViewToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var plans = await response.Content.ReadFromJsonAsync<IEnumerable<PlanDto>>();
    Assert.NotNull(plans);
    AssertSecurityHeaders(response);
  }

  // ==============================================================================================
  // GET /api/platform/plans/{planId} — authorization
  // ==============================================================================================

  [Fact]
  public async Task GetPlanById_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownPlanId}", token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetPlanById_without_view_permission_returns_403()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownPlanId}",
      Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // GET /api/platform/plans/{planId} — success and not-found
  // ==============================================================================================

  [Fact]
  public async Task GetPlanById_authorized_for_known_plan_returns_200_with_plan()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownPlanId}", ViewToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var plan = await response.Content.ReadFromJsonAsync<PlanDto>();
    Assert.NotNull(plan);
    Assert.Equal(KnownPlanId, plan!.SubscriptionPlanId);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task GetPlanById_authorized_for_unknown_plan_returns_400()
  {
    // The stub returns failure for any id that is not KnownPlanId.
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{Guid.NewGuid()}", ViewToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // POST /api/platform/plans — authorization
  // ==============================================================================================

  [Fact]
  public async Task CreatePlan_without_token_returns_401()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(repository.AddCalled);
  }

  [Fact]
  public async Task CreatePlan_without_administer_permission_returns_403()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(),
      Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(repository.AddCalled);
  }

  [Fact]
  public async Task CreatePlan_with_view_only_permission_returns_403()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(repository.AddCalled);
  }

  // ==============================================================================================
  // POST /api/platform/plans — strict JSON (closes StrictRequestReader M1 concern for Plans)
  // ==============================================================================================

  [Theory]
  [InlineData("{\"planName\":\"Test Plan\"}")] // missing planCode
  [InlineData("{\"planCode\":\"PLAN-01\"}")] // missing planName
  [InlineData("{\"planCode\":123,\"planName\":\"Test Plan\"}")] // numeric code
  [InlineData("{\"PlanCode\":\"PLAN-01\",\"planName\":\"Test Plan\"}")] // case mismatch
  [InlineData("{\"planCode\":\"PLAN-01\",\"planName\":\"Test\",\"extra\":\"value\"}")] // unknown field
  [InlineData("[]")] // non-object root
  [InlineData("{\"planCode\":")] // malformed JSON
  public async Task CreatePlan_strict_json_rejects_invalid_bodies_with_400(string body)
  {
    var response = await Client.SendAsync(Post(RoutePrefix, body, AdministerToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.False(repository.AddCalled);
    AssertSecurityHeaders(response);
  }

  // ==============================================================================================
  // POST /api/platform/plans — success
  // ==============================================================================================

  [Fact]
  public async Task CreatePlan_authorized_with_valid_body_returns_201_with_location()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), AdministerToken()));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.True(repository.AddCalled);
    Assert.Equal(1, unitOfWork.SaveCount);
    Assert.NotNull(response.Headers.Location);
    Assert.StartsWith(RoutePrefix, response.Headers.Location!.ToString(), StringComparison.Ordinal);
    AssertSecurityHeaders(response);
  }

  // ==============================================================================================
  // POST /api/platform/plans — domain validation
  // ==============================================================================================

  [Theory]
  [InlineData("{\"planCode\":\"\",\"planName\":\"Test Plan\"}")] // blank code
  [InlineData("{\"planCode\":\"PLAN-01\",\"planName\":\"   \"}")] // blank name
  public async Task CreatePlan_domain_validation_failure_returns_400(string body)
  {
    var response = await Client.SendAsync(Post(RoutePrefix, body, AdministerToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.False(repository.AddCalled);
  }

  [Fact]
  public async Task CreatePlan_duplicate_normalized_code_returns_400()
  {
    repository.CodeExists = true;

    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), AdministerToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.False(repository.AddCalled);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId} — authorization
  // ==============================================================================================

  [Fact]
  public async Task UpdatePlan_without_token_returns_401()
  {
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}", ValidUpdateBody(), token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task UpdatePlan_without_administer_permission_returns_403()
  {
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}", ValidUpdateBody(), ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId} — strict JSON
  // ==============================================================================================

  [Theory]
  [InlineData("{}")] // missing planName
  [InlineData("{\"planName\":123}")] // numeric name
  [InlineData("{\"PlanName\":\"Updated Plan\"}")] // case mismatch
  [InlineData("{\"planName\":\"Updated\",\"extra\":\"field\"}")] // unknown field
  public async Task UpdatePlan_strict_json_rejects_invalid_bodies_with_400(string body)
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}", body, AdministerToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId} — success
  // ==============================================================================================

  [Fact]
  public async Task UpdatePlan_authorized_with_valid_body_returns_200()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}", ValidUpdateBody(), AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task UpdatePlan_unknown_plan_returns_400()
  {
    // No plan seeded - repository returns null.
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{Guid.NewGuid()}", ValidUpdateBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // POST /api/platform/plans/{planId}/retire — authorization
  // ==============================================================================================

  [Fact]
  public async Task RetirePlan_without_token_returns_401()
  {
    SeedPlan();
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownPlanId}/retire", null, token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task RetirePlan_without_administer_permission_returns_403()
  {
    SeedPlan();
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownPlanId}/retire", null, ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // POST /api/platform/plans/{planId}/retire — success
  // ==============================================================================================

  [Fact]
  public async Task RetirePlan_authorized_returns_200_with_retired_plan()
  {
    SeedPlan();
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownPlanId}/retire", null, AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task RetirePlan_unknown_plan_returns_400()
  {
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{Guid.NewGuid()}/retire", null, AdministerToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId}/modules — authorization
  // ==============================================================================================

  [Fact]
  public async Task SetPlanModules_without_token_returns_401()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/modules",
      "[\"HR\",\"Payroll\"]", token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task SetPlanModules_without_administer_permission_returns_403()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/modules",
      "[\"HR\",\"Payroll\"]", ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId}/modules — success
  // ==============================================================================================

  [Fact]
  public async Task SetPlanModules_authorized_with_valid_array_returns_200()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/modules",
      "[\"HR\",\"Payroll\"]", AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task SetPlanModules_with_empty_array_clears_all_modules()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/modules",
      "[]", AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task SetPlanModules_unknown_plan_returns_400()
  {
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{Guid.NewGuid()}/modules",
      "[\"HR\"]", AdministerToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId}/limits — authorization
  // ==============================================================================================

  [Fact]
  public async Task SetPlanLimits_without_token_returns_401()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/limits",
      ValidLimitsBody(), token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task SetPlanLimits_without_administer_permission_returns_403()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/limits",
      ValidLimitsBody(), ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId}/limits — success
  // ==============================================================================================

  [Fact]
  public async Task SetPlanLimits_authorized_with_valid_body_returns_200()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/limits",
      ValidLimitsBody(), AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task SetPlanLimits_with_empty_array_clears_all_limits()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/limits",
      "[]", AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task SetPlanLimits_unknown_plan_returns_400()
  {
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{Guid.NewGuid()}/limits",
      ValidLimitsBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId}/prices — authorization
  // ==============================================================================================

  [Fact]
  public async Task SetPlanPrices_without_token_returns_401()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/prices",
      ValidPricesBody(), token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task SetPlanPrices_without_administer_permission_returns_403()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/prices",
      ValidPricesBody(), ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/plans/{planId}/prices — success
  // ==============================================================================================

  [Fact]
  public async Task SetPlanPrices_authorized_with_valid_body_returns_200()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/prices",
      ValidPricesBody(), AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task SetPlanPrices_with_empty_array_clears_all_prices()
  {
    SeedPlan();
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownPlanId}/prices",
      "[]", AdministerToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task SetPlanPrices_unknown_plan_returns_400()
  {
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{Guid.NewGuid()}/prices",
      ValidPricesBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // Persistence failure mapping
  // ==============================================================================================

  [Fact]
  public async Task CreatePlan_persistence_write_failure_maps_to_safe_500()
  {
    unitOfWork.Failure = new Error("Persistence.WriteFailure", "Write failed");
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), AdministerToken()));

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    var payload = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("Persistence", payload, StringComparison.Ordinal);
  }

  // ==============================================================================================
  // Lifecycle
  // ==============================================================================================

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(
      new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30"
    });

    repository = new StubPlanRepository();
    planQueries = new StubPlanQueries();
    repository.OnAdd = id => planQueries.AddedPlanId = id;
    unitOfWork = new StubPlatformUnitOfWork();

    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();

    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddSingleton<ISubscriptionPlanRepository>(repository);
    builder.Services.AddSingleton<IPlanQueries>(planQueries);
    builder.Services.AddSingleton<IPlatformUnitOfWork>(unitOfWork);

    // Register all handlers so the full route family resolves under Development validation.
    builder.Services.AddScoped<CreateSubscriptionPlanCommandHandler>();
    builder.Services.AddScoped<UpdateSubscriptionPlanCommandHandler>();
    builder.Services.AddScoped<RetireSubscriptionPlanCommandHandler>();
    builder.Services.AddScoped<SetPlanModulesCommandHandler>();
    builder.Services.AddScoped<SetPlanLimitsCommandHandler>();
    builder.Services.AddScoped<SetPlanPricesCommandHandler>();
    builder.Services.AddScoped<GetSubscriptionPlansQueryHandler>();
    builder.Services.AddScoped<GetSubscriptionPlanByIdQueryHandler>();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformPlansEndpoints();

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

  // ==============================================================================================
  // Helpers
  // ==============================================================================================

  private HttpClient Client => client ?? throw new InvalidOperationException("Test host has not started.");

  private void SeedPlan() => repository.SeedPlan(KnownPlanId);

  private static string ValidCreateBody() =>
    "{\"planCode\":\"PLAN-TEST\",\"planName\":\"Test Plan\"}";

  private static string ValidUpdateBody() =>
    "{\"planName\":\"Updated Plan Name\"}";

  private static string ValidLimitsBody() =>
    "[{\"limitKey\":\"Employees\",\"limitValue\":50}]";

  private static string ValidPricesBody() =>
    "[{\"currencyCode\":\"USD\",\"billingPeriod\":\"Monthly\",\"amount\":99.99}]";

  private static HttpRequestMessage Get(string path, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    return request;
  }

  private static HttpRequestMessage Post(string path, string? body, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, path);
    if (body is not null)
    {
      request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    return request;
  }

  private static HttpRequestMessage Put(string path, string? body, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Put, path);
    if (body is not null)
    {
      request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    return request;
  }

  private string ViewToken() => Token(
    new Claim(JwtClaimTypes.SecurityPlane, "platform"),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewPlans));

  private string AdministerToken() => Token(
    new Claim(JwtClaimTypes.SecurityPlane, "platform"),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.AdministerPlans));

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>()
      ?? throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(
      keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;
    
    var isPlatform = claims.Any(c => c.Type == JwtClaimTypes.SecurityPlane && c.Value == "platform");
    
    var requiredClaims = new List<Claim>
    {
      new Claim(JwtClaimTypes.Subject, "plan-admin"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(
        System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new Claim(JwtClaimTypes.IdentityId, "1"),
      new Claim(JwtClaimTypes.SessionId, "3"),
      new Claim(JwtClaimTypes.ClientId, "ssas-erp-web"),
      new Claim(JwtClaimTypes.SecurityVersion, "1")
    };
    
    if (!isPlatform)
    {
      requiredClaims.Add(new Claim(JwtClaimTypes.TenantUserId, "2"));
    }

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
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedCode)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
  }

  // ==============================================================================================
  // Stubs
  // ==============================================================================================

  private sealed class StubPlanRepository : ISubscriptionPlanRepository
  {
    private SubscriptionPlan? _seeded;
    public bool CodeExists { get; set; }
    public bool AddCalled { get; private set; }
    public Action<Guid>? OnAdd { get; set; }

    public void SeedPlan(Guid planId)
    {
      _seeded = SubscriptionPlan.Create(
        SSAS.Platform.Domain.ValueObjects.PlanCode.Create("SEED").Value,
        SSAS.Platform.Domain.ValueObjects.PlanName.Create("Seed").Value,
        "test",
        DateTimeOffset.UtcNow).Value;

      typeof(SSAS.BuildingBlocks.Domain.Entity<Guid>)
        .GetField("<Id>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .SetValue(_seeded, planId);
    }

    public Task<SubscriptionPlan?> GetByIdAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default)
      => Task.FromResult(_seeded);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedPlanCode, CancellationToken cancellationToken = default)
      => Task.FromResult(CodeExists);

    public Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
      AddCalled = true;
      OnAdd?.Invoke(plan.SubscriptionPlanId);
      return Task.CompletedTask;
    }
  }

  private sealed class StubPlanQueries : IPlanQueries
  {
    public Guid? AddedPlanId { get; set; }

    public Task<Result<IEnumerable<PlanDto>>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
      var plans = new List<PlanDto>
      {
        new(KnownPlanId, "PLAN-TEST", "Test Plan", SubscriptionPlanStatus.Draft, [], [], [])
      };
      return Task.FromResult(Result.Success<IEnumerable<PlanDto>>(plans));
    }

    public Task<Result<PlanDto>> GetPlanByIdAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default)
    {
      if (subscriptionPlanId == KnownPlanId || subscriptionPlanId == AddedPlanId)
      {
        var dto = new PlanDto(subscriptionPlanId, "PLAN-TEST", "Test Plan", SubscriptionPlanStatus.Draft, [], [], []);
        return Task.FromResult(Result.Success(dto));
      }

      return Task.FromResult(Result.Failure<PlanDto>(
        new Error("Subscription.InvalidPlanCode", "Plan not found")));
    }
  }

  private sealed class StubPlatformUnitOfWork : IPlatformUnitOfWork
  {
    public Error? Failure { get; set; }
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Failure is { } error
        ? Result.Failure<int>(error)
        : Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
      => throw new NotSupportedException();
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
      => GetEligibilityAsync(tenantId, cancellationToken);
  }
}
