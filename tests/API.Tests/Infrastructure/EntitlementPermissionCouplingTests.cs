using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Subscriptions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// ENTITLEMENT AND THE EFFECTIVE PERMISSION SET ARE NOT COUPLED (FP-014 item 162).
// ==================================================================================================
//
// `AC-SUB-0024`, `AC-SUB-0025` and `AC-SUB-0026` describe a coupling between commercial entitlement and
// a tenant's effective permissions: a permission for an unentitled module refused at grant time, and a
// role's permission ceasing to satisfy a check when entitlement lapses.
//
// ⚠ **NO SUCH COUPLING EXISTS, AND A SEARCH SAYING SO IS NOT EVIDENCE.** Item 161 could only report "no
// coupling was FOUND", which is a statement about the search. These tests exercise the path instead, and
// they are written so that a coupling introduced later REDDENS them rather than passing silently.
//
// ---- ⚠ WHY THE ZERO-CONSULTATION ASSERTION NEEDS THE MODULE-GATE CONTROL.
//
// "The permission check never consults entitlement" is trivially true if the reader is unreachable, the
// container is misbuilt, or the counter is never wired to anything. **So the SAME reader instance is
// driven through `RequireModule` in the control below and must come back consulted.** Without that, this
// file would assert the absence of a collaboration that was never possible in the first place — the
// vacuity that `ModuleEnablementCoverageTests` guards against for its own scan.
//
// ---- WHAT THIS FILE DOES NOT ESTABLISH.
//
// `AC-SUB-0026`'s "counts before and after" needs a persisted module table and an entitlement-lapse write
// path. **There is no write path**: entitlement is resolved by reading, and `HasExpiredAt` is a pure
// function of the term. The structural assertions below cover the reachable half — that nothing in the
// permission or granting path takes an entitlement dependency — and the row-count half is stated as open
// in the item's result file rather than asserted here.
public sealed class EntitlementPermissionCouplingTests
{
  private const string ModuleKey = "Payroll";
  private const string Permission = "test.permission";
  private static readonly Guid TenantId = Guid.Parse("2f6c1de6-9f22-4f8e-8b1a-5c7d0e3a9b41");
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  // A term that ended a day before the clock, so every entitlement question resolves to "lapsed".
  private static SubscriptionTerm LapsedTerm => SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value;

  private static DateTimeOffset AfterTheTerm => Noon.AddDays(31);

  // ---- THE DECISION IS UNCHANGED WHILE ENTITLEMENT IS LAPSED.
  [Fact]
  public async Task A_permission_check_succeeds_while_the_tenant_entitlement_has_lapsed()
  {
    var reader = new CountingReader(LapsedTerm);
    var context = Context(
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, Permission));

    await PermissionHandler().HandleAsync(context);

    Assert.True(context.HasSucceeded);
    Assert.Equal(0, reader.Reads);
  }

  // ---- AND IT NEVER ASKED.
  [Fact]
  public async Task The_permission_check_consults_the_entitlement_reader_zero_times()
  {
    var reader = new CountingReader(LapsedTerm);
    var context = Context(
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, Permission));

    await PermissionHandler().HandleAsync(context);

    Assert.Equal(0, reader.Reads);
  }

  // ---- ⚠ THE CONTROL. THE SAME READER TYPE, REACHED THROUGH `RequireModule`, IS CONSULTED.
  // Without this the two assertions above would hold for a reader nothing could ever call.
  [Fact]
  public async Task The_module_gate_consults_the_same_reader_which_is_what_stops_the_zero_being_vacuous()
  {
    var reader = new CountingReader(LapsedTerm);
    await using var application = await StartAsync(reader, AfterTheTerm);

    var response = await application.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.True(reader.Reads > 0, "the module gate did not consult the reader, so the zero above proves nothing");
  }

  // ---- THE SECOND CONTROL: THE PERMISSION GATE IS LIVE, NOT INERT.
  [Fact]
  public async Task The_permission_check_still_denies_without_the_claim_while_entitlement_is_lapsed()
  {
    var context = Context(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));

    await PermissionHandler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  // ---- ⚠ THE STRUCTURAL HALF, WHICH IS WHAT SURVIVES A REFACTOR.
  // An outcome test proves today's behaviour; this reddens the moment anyone gives either tenant
  // authorization handler an entitlement collaborator, whatever the resulting behaviour happens to be.
  [Theory]
  [InlineData(typeof(PermissionAuthorizationHandler))]
  [InlineData(typeof(RoleAuthorizationHandler))]
  public void No_tenant_authorization_handler_takes_an_entitlement_dependency(Type handler)
  {
    Assert.DoesNotContain(DependencyNames(handler), name => name.Contains("Entitlement", StringComparison.Ordinal));
  }

  // ---- `AC-SUB-0024`: THE GRANT PATH DOES NOT CONSULT ENTITLEMENT EITHER.
  [Fact]
  public void Granting_a_permission_to_a_role_takes_no_entitlement_dependency()
  {
    var handler = typeof(SSAS.Platform.Application.Roles.AssignPermissionToRoleCommandHandler);

    Assert.DoesNotContain(DependencyNames(handler), name => name.Contains("Entitlement", StringComparison.Ordinal));
  }

  private static IEnumerable<string> DependencyNames(Type type) =>
    type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name);

  private static AuthorizationHandlerContext Context(params Claim[] claims) =>
    new(
      [new PermissionRequirement(Permission)],
      new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
      resource: null);

  private static PermissionAuthorizationHandler PermissionHandler() =>
    new(
      new FixedTenant(),
      new LiveTenantEligibilityAuthorization(new ActiveRequestTenantEligibility()),
      new HttpContextAccessor());

  private static async Task<WebApplication> StartAsync(ITenantEntitlementReader reader, DateTimeOffset now)
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock(now));
    builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
    builder.Services.AddScoped(_ => reader);
    builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

    var application = builder.Build();
    application.MapGet("/gated", () => Results.Ok("reached")).RequireModule(ModuleKey);
    await application.StartAsync();

    return application;
  }

  private sealed class CountingReader(SubscriptionTerm term) : ITenantEntitlementReader
  {
    public int Reads { get; private set; }

    public Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
      Reads++;

      return Task.FromResult(new TenantEntitlementSnapshot(
        tenantId,
        Guid.NewGuid(),
        term,
        new HashSet<string>([ModuleKey], StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        []));
    }
  }

  private sealed class ActiveRequestTenantEligibility : IRequestTenantEligibility
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => EntitlementPermissionCouplingTests.TenantId;
  }

  private sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = now;
  }
}
