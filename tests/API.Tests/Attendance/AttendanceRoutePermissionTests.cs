using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.API.Tests.Infrastructure;

namespace SSAS.API.Tests.Attendance;

// ==================================================================================================
// PARITY: EVERY ATTENDANCE ROUTE REQUIRES A PERMISSION (T-076, T-077).
// ==================================================================================================
//
// ---- THE GAP THIS CLOSES, MEASURED RATHER THAN ARGUED.
//
// HR, GL and Payroll each assert this property — `HrRouteInventoryTests.cs:52`,
// `GlRouteInventoryTests.cs:60`, `PayrollRouteInventoryTests.cs:60`. **Attendance had no equivalent, and
// no test anywhere in the tree called an Attendance route at all** (T-076: `grep -rn "api/attendance"
// tests/` returned zero matches).
//
// So a route added to the Attendance group without `.RequirePermission(...)` was reachable by any caller,
// and **T-077 proved by planting one that the entire suite stayed green** — `[GATE GREEN]`, 2765 tests,
// nothing red. The only thing in the toolchain that noticed was the gate's own condition 4, which reports
// a `src/` change that moved no test total and does not fail.
//
// There is no fallback authorization policy to catch it either: the `RequireAuthenticatedUser()` at
// `PermissionAuthorizationPolicyProvider.cs:75` sits inside `CreatePolicyBuilder()`, which builds NAMED
// policies. It is not `AuthorizationOptions.FallbackPolicy`. An endpoint with no authorization metadata is
// anonymous.
//
// ---- WHY THIS IS NOT A ROUTE INVENTORY, DELIBERATELY.
//
// The other three modules assert this property inside a file that also pins an exact route list. An
// inventory catches an UNDOCUMENTED route; this catches an UNGUARDED one. They are different properties
// and the second is the one whose absence was measured, so it is the one built here.
//
// ---- IT ENUMERATES FROM THE REAL HOST.
//
// Attendance has no per-module test host, and building one to hold this would be a larger change than the
// property needs. `HostWebApplicationFactory` wraps the real `Program`, so this asserts the surface
// production actually mounts.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class AttendanceRoutePermissionTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/attendance";

  [Fact]
  public void Every_attendance_route_requires_a_permission()
  {
    var routes = AttendanceRoutes();

    // NOT VACUOUS, AND THIS LINE IS THE ONE THAT MATTERS MOST HERE. A prefix filter that stopped matching
    // — a mount point renamed, the module unregistered — would leave an empty set and a green loop, which
    // is precisely the "guard that cannot fail" this suite has now found three times.
    Assert.NotEmpty(routes);

    var unprotected = routes
      .Where(route => string.IsNullOrEmpty(Policy(route)))
      .Select(route => Describe(route))
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    // The message states what is true of the route, not why it happened. A route may lack a permission
    // because someone forgot `.RequirePermission(...)`, or because it was deliberately meant to be open —
    // and the second is a decision this guard is not entitled to recognise silently.
    Assert.True(
      unprotected.Length == 0,
      "An Attendance route requires no permission, so any caller who reaches the host reaches it. " +
      "Attendance records who was present and who took leave; there is no route here that should be " +
      $"open. Add `.RequirePermission(...)`, or state the exception deliberately:{Environment.NewLine}" +
      string.Join(Environment.NewLine, unprotected));
  }

  private RouteEndpoint[] AttendanceRoutes() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
      .OfType<RouteEndpoint>()
      .Where(endpoint =>
        endpoint.RoutePattern.RawText?.StartsWith(RoutePrefix, StringComparison.Ordinal) ?? false)
  ];

  private static string? Policy(RouteEndpoint endpoint) =>
    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
      .Select(data => data.Policy)
      .FirstOrDefault(policy => !string.IsNullOrEmpty(policy));

  private static string Describe(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
    var verb = methods is { Count: > 0 } ? string.Join("/", methods) : "?";

    return $"{verb} {endpoint.RoutePattern.RawText}";
  }
}
