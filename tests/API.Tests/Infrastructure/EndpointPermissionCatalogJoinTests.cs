using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE JOIN NOTHING ASSERTED: A PERMISSION AN ENDPOINT REQUIRES MUST BE ONE THE CATALOG DEFINES.
// ==================================================================================================
//
// ---- THE GAP, AND WHY THREE EXISTING GUARDS DID NOT CLOSE IT.
//
// FP-006P's incident was a permission an endpoint required and no catalog defined: nothing could grant it,
// every caller got 403, and every test passed — because tests mint permission claims directly and never
// travel the assignment path. Two guards were written afterwards and neither asserts that join:
//
//   PayrollRouteInventoryTests:74   a route requires the permission THE INVENTORY names   HR, GL, Payroll
//   EmployeeHostCompositionTests H11  the CONTRIBUTOR's names reach the composed catalog   HR only
//
// The first compares a route to a hand-written list. The second compares a contributor to the catalog. **No
// test compared a route to the catalog**, so a route and an inventory could agree with each other while both
// disagreed with the catalog — three places, two of which are the same document.
//
// Attendance has no route inventory at all and H11 is HR-only, so the two modules FP-015 touches sat outside
// both guards entirely.
//
// ---- WHY IT ENUMERATES INSTEAD OF LISTING.
//
// A hand-written expected list would be a THIRD document that can agree with the other two while all three
// disagree with the catalog — the defect being closed, reintroduced by the closing. Endpoints come from the
// real Host's `EndpointDataSource` and the catalog from the same container, so the guard is module-agnostic:
// it covers Attendance without an inventory and covers the next module before anyone remembers to add one.
//
// `TryGetPermissionName` is the inverse of the `CreatePolicyName` that built these policies, so the recovery
// is the production one rather than a second parser (`PermissionAuthorizationDefaults:20-37`).
public sealed class EndpointPermissionCatalogJoinTests(HostWebApplicationFactory factory)
  : IClassFixture<HostWebApplicationFactory>
{
  // ================================================================================================
  // THE JOIN.
  // ================================================================================================
  [Fact]
  public void Every_permission_an_endpoint_requires_is_defined_by_the_composed_catalog()
  {
    var catalog = factory.Services.GetRequiredService<IPermissionCatalog>();
    var required = RequiredPermissions();

    // NOT VACUOUS, ASSERTED FIRST. A filter that matches nothing produces a green loop over an empty set,
    // which is a guard that cannot fail — found twice in this repository already. If the policy spelling
    // changes and recovery stops matching, this line fails instead of the suite quietly passing.
    Assert.NotEmpty(required);

    var undefined = required
      .Where(entry => !catalog.TryGet(entry.Permission, out _))
      .Select(entry => $"{entry.Route} requires '{entry.Permission}', which no contributor defines")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    // The message states the QUESTION, not a cause: the guard sees a route and a catalog disagreeing and
    // cannot know which of the two is wrong. Either the permission was never contributed, or the endpoint
    // names it wrongly, and the reader has to look.
    Assert.True(
      undefined.Length == 0,
      "A route requires a permission the composed catalog does not define. Every caller receives 403 and " +
      "no role can be granted it, because a role may only hold a permission the catalog defines. Either " +
      "the permission is missing from its module's contributor, or the endpoint names one that does not " +
      $"exist:{Environment.NewLine}{string.Join(Environment.NewLine, undefined)}");
  }

  // ================================================================================================
  // NOTHING IS SWALLOWED: EVERY ENDPOINT IS ACCOUNTED FOR, AND THE RESIDUAL CATEGORY IS EMPTY.
  // ================================================================================================
  //
  // Anonymous endpoints are not failures — `/health` and the login routes are deliberately unauthenticated,
  // and a guard demanding a permission on every endpoint would be wrong. But "skip what does not match" is
  // how a filter silently stops examining anything, so the skips are ACCOUNTED rather than dropped: the
  // three buckets must sum to the endpoint total, which is the report a passing test can actually make.
  //
  // The third bucket is the one that matters. A policy that is neither a tenant nor a platform permission
  // policy is a route this guard does not examine, so it is asserted EMPTY and named if it is not — a future
  // policy shape has to be classified here rather than quietly falling outside the join.
  [Fact]
  public void Every_endpoint_is_classified_and_no_policy_escapes_the_join()
  {
    var endpoints = RouteEndpoints();

    var anonymous = endpoints
      .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
      .ToArray();

    var withPolicies = endpoints
      .Select(endpoint => (
        Route: Describe(endpoint),
        Policies: endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
          .Select(data => data.Policy)
          .Where(policy => !string.IsNullOrEmpty(policy))
          .ToArray()))
      .Where(entry => entry.Policies.Length > 0)
      .ToArray();

    var unclassified = withPolicies
      .SelectMany(entry => entry.Policies.Select(policy => (entry.Route, Policy: policy!)))
      .Where(entry =>
        !PermissionAuthorizationDefaults.TryGetPermissionName(entry.Policy, out _) &&
        !PlatformPermissionAuthorizationDefaults.TryGetPermissionName(entry.Policy, out _))
      .Select(entry => $"{entry.Route} carries policy '{entry.Policy}'")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      unclassified.Length == 0,
      "A policy on a mapped route is neither a tenant nor a platform permission policy, so the join above " +
      "does not examine the route at all. Classify it here — a route outside the join is a route whose " +
      $"permission nothing checks against the catalog:{Environment.NewLine}" +
      string.Join(Environment.NewLine, unclassified));

    // THE ACCOUNTING IDENTITY. Every mapped endpoint is either anonymous or carries at least one policy;
    // nothing falls between the two. An endpoint carrying `[Authorize]` with no policy would land in
    // neither bucket and fail here, which is correct: it is authenticated-only, reachable by any caller
    // with a token, and that is a decision someone has to make deliberately rather than by omission.
    var authenticatedOnly = endpoints
      .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0)
      .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
        .All(data => string.IsNullOrEmpty(data.Policy)))
      .Select(Describe)
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    // ---- THE THIRD BUCKET IS PINNED BY NAME, AND IT IS THE ONLY LIST IN THIS FILE.
    //
    // These four carry `.RequireAuthorization()` with no policy, so any authenticated caller reaches them
    // and the join above cannot examine them. All four are deliberate, verified at their map sites: logging
    // out must not require a permission or a user could be stranded holding a session they cannot end, and
    // effective localization is the UI's own strings, which every authenticated caller needs before they
    // hold any permission at all.
    //
    // Naming them rather than counting them is the point. A count passes when one is removed and another
    // added; naming means a FIFTH authenticated-only route has to be acknowledged here, by someone who has
    // to write down why it is not a permission-bearing route. That is the only way a route can leave the
    // join without anyone noticing, so it is the one place a hand-written list belongs.
    string[] expectedAuthenticatedOnly =
    [
      "GET /api/platform/localization/effective/",
      "POST /api/platform/auth/logout",
      "POST /api/platform/localization/effective/batch",
      "POST /api/platform/support/auth/logout"
    ];

    Assert.Equal(expectedAuthenticatedOnly, authenticatedOnly);

    // THE ACCOUNTING IDENTITY. Three buckets, summing to the whole: anonymous, policy-bearing, and
    // authenticated-only. Nothing falls between them, so no route can be skipped without changing one of
    // the three — which is the report a PASSING test can make, and the reason the skips are not simply
    // filtered away.
    Assert.Equal(
      endpoints.Length,
      anonymous.Length + withPolicies.Length + authenticatedOnly.Length);
  }

  private (string Route, string Permission)[] RequiredPermissions() =>
  [
    .. RouteEndpoints()
      .SelectMany(endpoint => endpoint.Metadata
        .GetOrderedMetadata<IAuthorizeData>()
        .Select(data => data.Policy)
        .Where(policy => !string.IsNullOrEmpty(policy))
        .Select(policy => (Route: Describe(endpoint), Policy: policy!)))
      .Select(entry => (
        entry.Route,
        Permission: TryRecoverPermission(entry.Policy, out var permission) ? permission : null))
      .Where(entry => entry.Permission is not null)
      .Select(entry => (entry.Route, entry.Permission!))
      .Distinct()
  ];

  // BOTH PLANES. A platform-support permission is catalog-defined exactly as a tenant one is, so a route on
  // either plane requiring an undefined name is the same defect and is caught by the same pass.
  private static bool TryRecoverPermission(string policy, out string permission) =>
    PermissionAuthorizationDefaults.TryGetPermissionName(policy, out permission) ||
    PlatformPermissionAuthorizationDefaults.TryGetPermissionName(policy, out permission);

  private RouteEndpoint[] RouteEndpoints() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
  ];

  private static string Describe(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata
      .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods;

    var verb = methods is { Count: > 0 } ? string.Join("/", methods) : "?";

    return $"{verb} {endpoint.RoutePattern.RawText}";
  }
}
