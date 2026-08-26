using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Subscriptions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// AN EXPIRED TENANT IS REFUSED A MODULE AND STILL REACHES THE PLATFORM PLANE (`DEC-L-033`, T-040).
// ==================================================================================================
//
// ---- BOTH HALVES, WITH THE REAL RESOLVER RATHER THAN A STUB.
//
// `TenantModuleEntitlement` is the type the Host registers. What is stubbed is the READ — a subscription
// row is a database row and this is not an integration test — and the CLOCK, because the whole point is
// that expiry is a function of time rather than of a write.
//
// ---- THE SECOND HALF IS THE ONE WORTH HAVING, AND IT IS STRUCTURAL.
//
// `DEC-L-033` exists because a lapsed customer who cannot log in cannot reach the page that would let
// them subscribe. **No special case makes that true**: the platform plane simply carries no module key
// (`REQ-SUB-0013`), so `RequireModule` is never applied to it and the resolver is never consulted.
//
// The ungated route below asserts exactly that — not that it returns 200, but that **entitlement is
// never asked**. A route that succeeded because the resolver happened to say yes would pass a weaker
// test and fail the day the answer changed.
public sealed class ExpiredTenantGateTests
{
  private const string ModuleKey = "Payroll";
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid TenantId = Guid.NewGuid();

  private static async Task<WebApplication> StartAsync(SubscriptionTerm term, DateTimeOffset now)
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();

    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(now));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader>(_ => new StubReader(term));
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    var application = builder.Build();
    application.MapGet("/gated", () => Results.Ok("reached")).RequireModule(ModuleKey);
    application.MapGet("/platform-plane", () => Results.Ok("reached"));

    await application.StartAsync();
    return application;
  }

  // ---- AN EXPIRED TERM: 403 ON THE MODULE.
  [Fact]
  public async Task An_expired_tenant_is_refused_a_gated_route_with_403()
  {
    await using var application = await StartAsync(
      SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value, now: Noon.AddDays(31));

    var response = await application.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AND THE SAME TENANT REACHES THE PLATFORM PLANE, WITHOUT ENTITLEMENT BEING CONSULTED.
  [Fact]
  public async Task An_expired_tenant_reaches_a_platform_plane_route_and_is_never_asked()
  {
    var reader = new StubReader(SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value);

    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(Noon.AddDays(31)));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader>(_ => reader);
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    await using var application = builder.Build();
    application.MapGet("/platform-plane", () => Results.Ok("reached"));
    await application.StartAsync();

    var response = await application.GetTestClient()
      .GetAsync(new Uri("/platform-plane", UriKind.Relative));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // The stronger claim: the resolver was never reached, so the outcome does not depend on what it
    // would have answered.
    Assert.Equal(0, reader.Reads);
  }

  // ---- BEFORE THE TERM ENDS, THE SAME HOST ADMITS THE SAME ROUTE.
  //
  // Without this, the 403 above would be satisfied by a gate that refused everything.
  [Fact]
  public async Task A_tenant_within_its_term_reaches_the_gated_route()
  {
    await using var application = await StartAsync(
      SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value, now: Noon.AddDays(29));

    var response = await application.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ---- A TENANT WITH NO SUBSCRIPTION REACHES NO GATED MODULE, AND THAT IS THE INTERIM STATE.
  //
  // Correct under `CON-0001` — no backfill, no default plan — and deliberately not softened here. T-041
  // seeds the 14-day trial `DEC-L-034` ruled, and that is what makes this safe rather than a lockout.
  [Fact]
  public async Task A_tenant_with_no_subscription_is_refused_and_that_is_the_ruled_interim_state()
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(Noon));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader>(_ => new StubReader(term: null));
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    await using var application = builder.Build();
    application.MapGet("/gated", () => Results.Ok("reached")).RequireModule(ModuleKey);
    await application.StartAsync();

    var response = await application.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- THE CACHE DOES NOT RESCUE AN EXPIRED TENANT, END TO END.
  //
  // The snapshot is read once and cached; the second request crosses the term boundary with **no write
  // and no invalidation**. This is the clock-advance proof at the HTTP surface rather than in isolation.
  [Fact]
  public async Task A_request_before_expiry_caches_a_snapshot_that_still_refuses_after_it()
  {
    var reader = new StubReader(SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value);
    var clock = new MovableClock(Noon.AddDays(29));

    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(clock);
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader>(_ => reader);
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    await using var application = builder.Build();
    application.MapGet("/gated", () => Results.Ok("reached")).RequireModule(ModuleKey);
    await application.StartAsync();

    var client = application.GetTestClient();

    Assert.Equal(HttpStatusCode.OK,
      (await client.GetAsync(new Uri("/gated", UriKind.Relative))).StatusCode);
    Assert.Equal(1, reader.Reads);

    clock.Now = Noon.AddDays(31);

    Assert.Equal(HttpStatusCode.Forbidden,
      (await client.GetAsync(new Uri("/gated", UriKind.Relative))).StatusCode);

    // Still one read: the entry was never evicted and nothing was written. The refusal came from
    // evaluating the cached term against the advanced clock.
    Assert.Equal(1, reader.Reads);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => ExpiredTenantGateTests.TenantId;
  }

  private sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = now;
  }

  private sealed class MovableClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset Now { get; set; } = now;

    public DateTimeOffset UtcNow => Now;
  }

  private sealed class StubReader(SubscriptionTerm? term) : ITenantEntitlementReader
  {
    public int Reads { get; private set; }

    public Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
      Reads++;

      if (term is null)
      {
        return Task.FromResult(TenantEntitlementSnapshot.None(tenantId));
      }

      return Task.FromResult(new TenantEntitlementSnapshot(
        tenantId, Guid.NewGuid(), term,
        new HashSet<string>([ModuleKey], StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal), []));
    }
  }
}
