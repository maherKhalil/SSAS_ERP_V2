using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Attendance.API;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Payroll.API;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE SELF-SERVICE ROUTES CARRY THE MODULE GATE — `AC-SS-0013`, `AC-SS-0014` (item 209).
// ==================================================================================================
//
// ---- ⚠ WHAT WAS ALREADY PROVEN, AND WHAT WAS NOT.
//
// `ModuleEnablementGateTests` proves the gate REFUSES an unentitled tenant with 403, and
// `ExpiredTenantGateTests` proves an expired subscription is refused while the platform plane stays
// reachable (`DEC-L-033`). **Both assert against a synthetic `/gated` probe route.**
//
// `PayrollEndpointRouteBuilderExtensions` and its Attendance twin say in source that the self routes *"sit
// in the same group as everything above, so `RequireModule` and the `BR-PLT-0008` gate come free"*.
//
// **So the gate was proven and the membership was commented, and their CONJUNCTION was asserted nowhere.**
// Item 207 recorded `AC-SS-0013`/`0014` as implemented and structurally guaranteed, but deliberately not
// as pinned, because *"no test says the SELF route specifically is refused when the module is off."*
// **This is that test.**
//
// ---- WHY IT ASSERTS METADATA RATHER THAN A 403.
//
// A 403 would need a host serving the REAL self routes with entitlement answering false. The module hosts
// register `AlwaysEntitled` through `ModuleEndpointHostRequirements` and share one fixture per class, so
// there is no per-test override; building a second host that maps real module endpoints would duplicate
// their whole dependency graph to re-prove what `ModuleEnablementGateTests` already proves.
//
// **`RequireModule` attaches `ModuleEnablementMetadata` to every endpoint it gates.** Asserting that
// metadata on the real, running route is the composition claim exactly: *this specific route is inside the
// gated group*. ⚠ **The gate's BEHAVIOUR is not re-proven here and must not be — that would be the probe
// route's test written twice.**
[Collection(HostIntegrationTestGroup.Name)]
public sealed class SelfServiceModuleGateTests(HostWebApplicationFactory factory)
{
  // ⚠ THE THREE SELF-SERVICE ROUTES, NAMED. A count would pass while one of them silently left the group.
  [Theory]
  [Trait("Criterion", "AC-SS-0013")]
  [Trait("Criterion", "AC-SS-0014")]
  [InlineData("/api/payroll/me/payslips", PayrollModuleEnablement.Key)]
  [InlineData("/api/attendance/me/records", AttendanceModuleEnablement.Key)]
  [InlineData("/api/attendance/me/leave-requests", AttendanceModuleEnablement.Key)]
  public void A_self_service_route_is_gated_by_its_module_entitlement(string pattern, string moduleKey)
  {
    var endpoint = Routes().SingleOrDefault(route =>
      string.Equals(route.RoutePattern.RawText, pattern, StringComparison.Ordinal));

    // ⚠ THE ROUTE MUST EXIST BEFORE ITS METADATA MEANS ANYTHING. A renamed or removed self route would
    // otherwise make this test pass over nothing at all -- which is the vacuity that retired `DEC-L-030`'s
    // guard, and it is cheap to exclude here.
    Assert.True(endpoint is not null, $"no route is mapped at '{pattern}', so its gating cannot be asserted.");

    var gate = endpoint!.Metadata.GetMetadata<ModuleEnablementMetadata>();

    Assert.True(
      gate is not null,
      $"""
      '{pattern}' CARRIES NO MODULE GATE, so AC-SS-0013 and AC-SS-0014 no longer hold.

      A self-service route is gated exactly as every other route of its module -- BR-PLT-0008 applies
      unchanged and there is no special case. It gets that by sitting inside the group that calls
      RequireModule, so the usual cause of this failure is a route moved out of the group, or mapped on
      `endpoints` directly instead of on `group`.

      Fix the mapping rather than this test: a tenant without the module, or one whose subscription has
      expired, must reach no self-service surface.
      """);

    Assert.Equal(moduleKey, gate!.ModuleKey);
  }

  // ==================================================================================================
  // ⚠ THE CONTROL: THE MATCHER MUST FIND A GATE IT IS NOT LOOKING FOR.
  // ==================================================================================================
  //
  // If `GetMetadata<ModuleEnablementMetadata>` returned null for every endpoint -- a renamed metadata type,
  // a convention that stopped being applied, a host that mapped no module routes -- the theory above would
  // fail loudly rather than pass, so it is not vacuous in the usual direction.
  //
  // **The risk is the opposite one: that these three are the ONLY gated routes**, which would mean the
  // gate is being applied per-route rather than per-group and the "comes free" argument in source is
  // false. This pins the group-level application by finding gated routes the self routes know nothing
  // about.
  [Fact]
  [Trait("Criterion", "AC-SS-0013")]
  public void The_module_gate_is_applied_to_the_whole_group_not_only_to_self_routes()
  {
    var gated = Routes()
      .Where(route => route.Metadata.GetMetadata<ModuleEnablementMetadata>() is not null)
      .Select(route => route.RoutePattern.RawText ?? string.Empty)
      .ToArray();

    // Far more than the three self routes, and including administrative ones.
    Assert.True(
      gated.Length > 3,
      $"only {gated.Length} gated route(s) found, so the gate is not being applied at group level.");

    Assert.Contains(gated, pattern => pattern.StartsWith("/api/payroll/", StringComparison.Ordinal)
      && !pattern.Contains("/me/", StringComparison.Ordinal));
  }

  private RouteEndpoint[] Routes() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
  ];
}
