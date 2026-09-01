using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// SHARED ENUMERATION FOR PLATFORM'S ROUTE INVENTORIES (T-129).
// ==================================================================================================
//
// **The four tenant-module inventories each carry their own private copy of these two helpers.** That is
// the shape T-127 found in Localization's strict reader and removed — **so the three Platform inventories
// added in T-129 share one instead of making it seven copies.**
//
// ---- ⚠ THE EXISTING FOUR WERE NOT MIGRATED, AND THAT IS A DELIBERATE STOP.
//
// `AttendanceRouteInventoryTests`, `HrRouteInventoryTests`, `GlRouteInventoryTests` and
// `PayrollRouteInventoryTests` still hold their own copies. **Migrating them is a separate task** — it
// touches four green guards to change nothing observable, which is the kind of edit that belongs on its own
// branch where a reviewer can see it.
//
// **Recorded rather than left as a seam somebody discovers**: this is 3-of-7 adoption, and T-127's lesson is
// that the un-migrated remainder is invisible until someone goes looking.
internal static class PlatformRouteInventory
{
  public static RouteEndpoint[] Under(HostWebApplicationFactory factory, string prefix) =>
    Under(factory.Services, prefix);

  // ---- THE SAME DERIVATION FROM ANY HOST'S SERVICES (243 step 2).
  //
  // `PlatformSupportAuthorityAuthorizationTests` builds its OWN minimal host -- application handlers are
  // deliberately unregistered there, so a request that reached one would surface as a DI failure rather
  // than passing. It maps the same surface through `MapPlatformSupportAuthorityEndpoints`, and deriving
  // its routes from ITS OWN endpoint source is stricter than borrowing another host's: the routes then
  // come from the very application the requests are sent to.
  public static RouteEndpoint[] Under(IServiceProvider services, string prefix) =>
  [
    .. services.GetRequiredService<EndpointDataSource>().Endpoints
      .OfType<RouteEndpoint>()
      .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.Ordinal) ?? false)
  ];

  public static string FirstMethodOf(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }

  // ---- AUTHORIZATION PRESENCE, SEPARATE FROM THE POLICY STRING (T-128).
  //
  // `IAuthorizeData.Policy` is **null both for a route with no authorization at all and for one carrying
  // `.RequireAuthorization()` with no named policy.** Asserting the string alone cannot tell a route that
  // lost its gate from one that deliberately has no permission.
  public static (bool HasAuthorization, string? Policy) AuthorizationOf(RouteEndpoint endpoint) =>
    (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0,
      endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
}
