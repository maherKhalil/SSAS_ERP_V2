using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.API.Tests.Infrastructure;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Localization;

// ==================================================================================================
// PLATFORM LOCALIZATION'S ROUTE INVENTORY (T-128) — **THE FIRST ONE PLATFORM HAS EVER HAD.**
// ==================================================================================================
//
// Attendance, HR, GL and Payroll each carry one. **Platform's SEVEN route groups carried none**, and that
// was not a considered exemption — the guard was simply never extended past the tenant modules.
//
// ---- WHY THIS GROUP FIRST, AND IT IS NOT BECAUSE ITS ROUTES ARE THE MOST IMPORTANT.
//
// **It is the group with the most VARIED permission surface**, which is the dimension a set comparison
// cannot check and the one an inventory exists to pin. Nine routes carry four different answers:
//
//   View / ViewHistory / Manage        six routes, three permissions
//   authenticated, no permission       two routes, and the reason is below
//
// `PlatformSupportAuthority` also has nine routes — **but all nine carry the same permission**, so a
// mis-gated route there is not expressible. Company's seven carry three. **This group can express the
// mistake the guard is for**, which makes it the honest first test of whether the shape ports to Platform.
//
// ---- ⚠ THE EXPECTED LIST WAS DERIVED FROM THE RUNNING SURFACE, SO A GREEN FIRST RUN PROVES NOTHING.
//
// **This is `DEC-L-070` and it is unavoidable for a new inventory**: an expectation read off the actual
// endpoints is guaranteed to match them. **The green is an artefact of the method, not evidence.**
//
// **Both properties were therefore planted before this file was committed** — a route removed, and a
// permission swapped — and both failed. That, not the green, is why these tests are believed to work.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class PlatformLocalizationRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/platform/localization";

  // ---- AUTHENTICATED, BUT DELIBERATELY CARRYING NO PERMISSION.
  //
  // `.RequireAuthorization()` with no policy. Not a gap: **every signed-in user must be able to read the
  // strings their own UI renders**, and a permission gate on that would make the login screen's own labels
  // conditional on a grant the user cannot have yet.
  //
  // Written as a marker rather than an empty string so that a route losing its gate ENTIRELY cannot pass
  // by looking like this row.
  private const string AuthenticatedWithoutPermission = "(authenticated, no permission — deliberate)";

  // ---- PINNED BY NAME, NOT BY COUNT. A count passes a swap; naming does not.
  //
  // ⚠ THE TRAILING SLASHES ARE REAL AND MUST NOT BE "FIXED". `MapGroup(prefix).MapGet("")` yields a
  // `RawText` of `.../resources/`, and routing matches the slash-less URL clients actually send — fifteen
  // existing tests call `/api/platform/companies` against the same shape and pass. **These rows record what
  // the pattern IS, not what the caller types.**
  private static readonly (string Method, string Pattern, string Policy)[] Expected =
  [
    // ---- THE CATALOG. Reading a resource and reading its history are separate grants, because history
    // ---- exposes prior override VALUES and therefore prior business wording.
    ("GET", "/api/platform/localization/resources/",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ViewLocalization),
    ("GET", "/api/platform/localization/resources/{resourceKey}",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ViewLocalization),
    ("GET", "/api/platform/localization/resources/{resourceKey}/history",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ViewLocalizationHistory),

    // ---- THE OVERRIDES. All three mutate tenant-visible wording, so all three carry `Manage` —
    // ---- including `undo` and `restore-default`, which REMOVE an override and are still writes.
    ("PUT", "/api/platform/localization/resources/{resourceKey}/overrides/{culture}",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ManageLocalization),
    ("POST", "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ManageLocalization),
    ("POST", "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ManageLocalization),

    // ---- PREVIEW. A read-shaped route on `Manage`, and that is deliberate: it renders a CANDIDATE
    // ---- override that has not been saved, so it is part of authoring rather than of reading.
    ("POST", "/api/platform/localization/preview",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ManageLocalization),

    // ---- EFFECTIVE. What the UI renders. See the marker above.
    ("GET", "/api/platform/localization/effective/", AuthenticatedWithoutPermission),
    ("POST", "/api/platform/localization/effective/batch", AuthenticatedWithoutPermission)
  ];

  [Fact]
  public void The_platform_localization_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = Routes()
      .Select(route => (Method: FirstMethodOf(route), Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    // NOT VACUOUS. A renamed mount point would leave an empty set on both sides of the comparison.
    Assert.NotEmpty(actual);

    var expected = Expected
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void Every_route_requires_the_permission_the_inventory_names()
  {
    // ---- THE PROPERTY THE SET COMPARISON CANNOT SEE.
    //
    // A route can be present, correctly named, and gated on the WRONG permission. `ViewLocalization` where
    // `ManageLocalization` belongs would satisfy every assertion above while handing override authorship to
    // any reader — the same mistake T-099's plant demonstrated in Attendance with `ViewLeave`/`ApproveLeave`.
    var actual = Routes().ToDictionary(
      route => $"{FirstMethodOf(route)} {route.RoutePattern.RawText}",
      route => new
      {
        HasAuthorization = route.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0,
        Policy = route.Metadata.GetMetadata<IAuthorizeData>()?.Policy
      },
      StringComparer.Ordinal);

    foreach (var (method, pattern, policy) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");

      // ---- EVERY ROUTE IS AUTHORIZED, INCLUDING THE TWO WITH NO PERMISSION.
      //
      // Asserted separately and FIRST, because a route that loses `.RequireAuthorization()` altogether
      // reports a null policy — which is indistinguishable from the deliberate no-permission rows unless
      // the presence of the metadata is checked on its own.
      Assert.True(actual[key].HasAuthorization, $"{key} carries no authorization metadata at all");

      if (policy == AuthenticatedWithoutPermission)
      {
        Assert.Null(actual[key].Policy);
        continue;
      }

      Assert.Equal(policy, actual[key].Policy);
    }
  }

  // ================================================================================================
  // ⚠ AND WHAT THIS INVENTORY CANNOT TELL YOU, RECORDED SO NOBODY READS ITS GREEN AS WIDER.
  // ================================================================================================
  //
  // **It says nothing about the 16 of Platform's 28 permissions that no route requires** (T-128), nor about
  // the 29 Platform handlers named nowhere in `SSAS.Platform.API`. Three whole administrative surfaces —
  // tenants, roles, users beyond de/reactivation — are permissioned, handled, and have no transport.
  //
  // **An inventory lists what EXISTS. It is structurally incapable of noticing what was never built**, which
  // is the one thing worth knowing about Platform right now. That belongs to a different guard, and naming
  // the limit here is cheaper than someone inferring coverage this file never claimed.
  private RouteEndpoint[] Routes() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
      .OfType<RouteEndpoint>()
      .Where(endpoint =>
        endpoint.RoutePattern.RawText?.StartsWith(RoutePrefix, StringComparison.Ordinal) ?? false)
  ];

  private static string FirstMethodOf(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }
}
