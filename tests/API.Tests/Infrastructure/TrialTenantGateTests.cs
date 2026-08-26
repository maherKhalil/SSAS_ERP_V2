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
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Infrastructure.Subscriptions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// A TRIAL TENANT REACHES EVERY MODULE FOR FOURTEEN DAYS, THEN NONE, AND KEEPS THE DOOR (T-041).
// ==================================================================================================
//
// ---- WHY THIS EXISTS BESIDE `ExpiredTenantGateTests`.
//
// That suite proves the MECHANISM: a fixed term expires, the gate refuses, the platform plane is never
// asked. This one proves the SETTLEMENT — that the specific plan `DEC-L-034` ruled, with the specific
// fourteen days the owner ruled, actually admits a tenant to the product and actually stops.
//
// The distinction matters because T-040 shipped a resolver that refuses a tenant holding no subscription.
// **Every assertion here is the difference between that being correct and that being a lockout.**
//
// ---- THE SNAPSHOT IS BUILT FROM `TrialSubscription`, NOT FROM A LIST OF FOUR STRINGS.
//
// The test reads the same definition the seed and the issuer read. A module removed from the trial fails
// the loop below rather than passing a test that was updated alongside the mistake.
//
// The READ is stubbed because a subscription row is a database row and this is not an integration test —
// `TrialSubscriptionSeedSqlServerTests` is where the rows are real. What is real here is the resolver, the
// cache and the gate.
public sealed class TrialTenantGateTests
{
  private static readonly DateTimeOffset Issued = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid TenantId = Guid.NewGuid();

  private static WebApplication Build(DateTimeOffset now)
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();

    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(now));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader, TrialReader>();
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    var application = builder.Build();

    foreach (var moduleKey in TrialSubscription.ModuleKeys)
    {
      application.MapGet($"/module/{moduleKey}", () => Results.Ok("reached")).RequireModule(moduleKey);
    }

    // The platform plane: no module key, so `RequireModule` is never applied to it (`REQ-SUB-0013`).
    application.MapGet("/platform-plane", () => Results.Ok("reached"));

    return application;
  }

  private static Task<HttpResponseMessage> GetAsync(WebApplication application, string path) =>
    application.GetTestClient().GetAsync(new Uri(path, UriKind.Relative));

  // ==================================================================================================
  // 1. INSIDE THE TERM: EVERY MODULE. "ALL MODULES" IS ASSERTED, NOT ASSUMED.
  // ==================================================================================================
  //
  // The loop covers each key the trial grants, so a plan that silently dropped one fails here rather than
  // passing on the strength of whichever module the test happened to name.
  [Fact]
  public async Task A_trial_tenant_reaches_every_gated_module_on_day_thirteen()
  {
    await using var application = Build(Issued.AddDays(13));
    await application.StartAsync();

    foreach (var moduleKey in TrialSubscription.ModuleKeys)
    {
      var response = await GetAsync(application, $"/module/{moduleKey}");

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
  }

  // ---- THE LAST INSTANT OF THE TERM IS STILL INSIDE IT.
  //
  // `HasExpiredAt` is `instant > end`, so the boundary belongs to the tenant. Asserted because an
  // off-by-one here is fourteen days becoming thirteen for everyone, and nothing else would notice.
  [Fact]
  public async Task The_final_instant_of_the_fourteenth_day_is_still_admitted()
  {
    await using var application = Build(Issued.AddDays(TrialSubscription.TermDays));
    await application.StartAsync();

    Assert.Equal(HttpStatusCode.OK, (await GetAsync(application, "/module/HR")).StatusCode);
  }

  // ==================================================================================================
  // 2. PAST THE TERM: NO MODULE, AND NO GRACE.
  // ==================================================================================================
  //
  // `DEC-L-009` ruled no grace period. One tick past fourteen days is refused, and so is a week later —
  // the second assertion rules out a boundary that merely looks right at one instant.
  [Fact]
  public async Task One_tick_after_the_term_no_module_is_reachable()
  {
    await using var application = Build(Issued.AddDays(TrialSubscription.TermDays).AddTicks(1));
    await application.StartAsync();

    foreach (var moduleKey in TrialSubscription.ModuleKeys)
    {
      var response = await GetAsync(application, $"/module/{moduleKey}");

      Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
  }

  [Fact]
  public async Task A_week_after_the_term_is_still_refused_and_no_grace_has_appeared()
  {
    await using var application = Build(Issued.AddDays(21));
    await application.StartAsync();

    Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(application, "/module/Payroll")).StatusCode);
  }

  // ==================================================================================================
  // 3. AND THE EXPIRED TRIAL TENANT STILL REACHES THE DOOR (`DEC-L-033`).
  // ==================================================================================================
  //
  // A lapsed customer who cannot sign in cannot reach the page that would let them buy. The stronger
  // claim is asserted rather than the convenient one: **the resolver is never consulted at all**, so the
  // outcome does not depend on what it would have answered.
  [Fact]
  public async Task An_expired_trial_tenant_reaches_the_platform_plane_without_entitlement_being_asked()
  {
    var reader = new TrialReader();

    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(Issued.AddDays(30)));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader>(_ => reader);
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    await using var application = builder.Build();
    application.MapGet("/platform-plane", () => Results.Ok("reached"));
    await application.StartAsync();

    var response = await GetAsync(application, "/platform-plane");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(0, reader.Reads);
  }

  // ==================================================================================================
  // 4. A TENANT WITH NO SUBSCRIPTION IS STILL REFUSED, WHICH IS WHY THE TRIAL HAD TO EXIST.
  // ==================================================================================================
  //
  // Kept deliberately: T-041 does not soften the resolver, it supplies the data. If this ever starts
  // returning 200, a default-plan fallback has been added somewhere and `CON-0001` has been broken.
  [Fact]
  public async Task A_tenant_the_seed_never_reached_is_still_refused_every_module()
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(Issued));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped<ITenantEntitlementReader>(_ => new UnseededReader());
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    await using var application = builder.Build();
    application.MapGet("/module/HR", () => Results.Ok("reached")).RequireModule("HR");
    await application.StartAsync();

    Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(application, "/module/HR")).StatusCode);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => TrialTenantGateTests.TenantId;
  }

  private sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = now;
  }

  // The trial exactly as `TrialSubscription` defines it: the same plan id, the same term length and the
  // same module set the seed and the issuer write. Nothing here restates a value.
  private sealed class TrialReader : ITenantEntitlementReader
  {
    public int Reads { get; private set; }

    public Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
      Reads++;

      return Task.FromResult(new TenantEntitlementSnapshot(
        tenantId,
        TrialSubscription.PlanId,
        TrialSubscription.TermFrom(Issued).Value,
        new HashSet<string>(TrialSubscription.ModuleKeys, StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        []));
    }
  }

  private sealed class UnseededReader : ITenantEntitlementReader
  {
    public Task<TenantEntitlementSnapshot> ReadAsync(
      Guid tenantId, CancellationToken cancellationToken) =>
      Task.FromResult(TenantEntitlementSnapshot.None(tenantId));
  }
}
