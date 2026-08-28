using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

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
[Collection(HostIntegrationTestGroup.Name)]
public sealed class EndpointPermissionCatalogJoinTests(HostWebApplicationFactory factory)
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
  // THE SECOND WAY A CORRECT-LOOKING ROUTE REFUSES EVERYONE: THE PLANE AND THE SCOPE DISAGREE.
  // ================================================================================================
  //
  // The join above asks whether the name EXISTS. This asks whether the caller the route is mapped for can
  // ever HOLD it. Both failures look identical from outside — a route that refuses every caller while every
  // line of it reads correctly — and the two facts needed to tell them apart were both already present and
  // never compared:
  //
  //   PermissionAuthorizationPolicyProvider.cs:14,19   the prefix chooses the plane
  //   TenantPermissionClaimFilter.cs:27-28             a non-Tenant permission never becomes a tenant claim
  //
  // ---- TENANT PREFIX ON A PlatformSupport PERMISSION.
  //
  // The catalog defines the name, so the join is satisfied. But `TenantPermissionClaimFilter` drops it from
  // every tenant token before it becomes a claim, and `PermissionAuthorizationHandler` matches on claims —
  // so no tenant caller can ever satisfy the requirement. **Not a theoretical hole: the filter exists
  // precisely to make it impossible**, which means a route asking for it is asking for something the system
  // guarantees will not arrive.
  //
  // ---- PLATFORM PREFIX ON A Tenant PERMISSION.
  //
  // `PlatformPermissionAuthorizationHandler.cs:28` checks `permission.Scope == PermissionScope.PlatformSupport`
  // explicitly, so the requirement fails for a Tenant-scoped name however the token is composed.
  //
  // **Both directions are asserted, and the second has no violating instance today.** A direction asserted
  // with no current instance is the one that catches the future mistake — the same reason a clean-tree
  // report is printed rather than omitted. If it is ever removed for looking redundant, that is the removal
  // this comment exists to argue with.
  [Fact]
  public void Every_route_is_mapped_on_the_plane_its_permission_is_scoped_to()
  {
    var catalog = factory.Services.GetRequiredService<IPermissionCatalog>();

    var known = RequiredPermissions()
      .Select(entry => (
        entry.Route,
        entry.DeclaredScope,
        Defined: catalog.TryGet(entry.Permission, out var definition) ? definition : null,
        entry.Permission))
      .Where(entry => entry.Defined is not null)
      .ToArray();

    // NOT VACUOUS, AND PER DIRECTION. A single non-empty check would pass while one whole plane went
    // unexamined, which is the failure mode this file has already found twice in other guards.
    var tenantPlane = known.Where(entry => entry.DeclaredScope == PermissionScope.Tenant).ToArray();
    var platformPlane = known.Where(entry => entry.DeclaredScope == PermissionScope.PlatformSupport).ToArray();

    Assert.NotEmpty(tenantPlane);
    Assert.NotEmpty(platformPlane);

    var tenantPlaneHoldingPlatformPermission = tenantPlane
      .Where(entry => entry.Defined!.Scope != PermissionScope.Tenant)
      .Select(entry =>
        $"{entry.Route} is mapped on the TENANT plane but '{entry.Permission}' is scoped " +
        $"{entry.Defined!.Scope}; TenantPermissionClaimFilter drops it from every tenant token")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    var platformPlaneHoldingTenantPermission = platformPlane
      .Where(entry => entry.Defined!.Scope != PermissionScope.PlatformSupport)
      .Select(entry =>
        $"{entry.Route} is mapped on the PLATFORM plane but '{entry.Permission}' is scoped " +
        $"{entry.Defined!.Scope}; PlatformPermissionAuthorizationHandler requires PlatformSupport")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    // The message names the disagreement and not a remedy. Which of the two is wrong — the plane the route
    // was mapped on, or the scope the permission carries — is an authorization decision, and a test that
    // asserted one of them was the correct half would be claiming a cause it cannot know.
    Assert.True(
      tenantPlaneHoldingPlatformPermission.Length == 0 && platformPlaneHoldingTenantPermission.Length == 0,
      "A route's plane and its permission's scope disagree, so no caller on that plane can ever hold it. " +
      "The route refuses everyone while reading correctly. Either the permission's scope or the plane the " +
      $"route is mapped on is wrong, and which one is an authorization decision:{Environment.NewLine}" +
      string.Join(
        Environment.NewLine,
        tenantPlaneHoldingPlatformPermission.Concat(platformPlaneHoldingTenantPermission)));
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

    // ---- THE ANONYMOUS BUCKET IS PINNED BY NAME TOO, AND IT WAS LEFT AS ARITHMETIC BY MISTAKE (T-077).
    //
    // The authenticated-only four below were named on the argument that "a count passes when one is
    // removed and another added". **That argument applies here identically and this bucket was left as a
    // count anyway** — so a route with no authorization metadata at all simply raised `anonymous` by one,
    // the three-bucket identity still balanced, and this test stayed green.
    //
    // **Measured, not reasoned:** T-077 planted a permission-less Attendance route and read the buckets
    // out of the running Host — `total=148 anonymous=10 policy=134 authOnly=4`. The identity held, and
    // the planted route sat in this list between `/` and the login routes.
    //
    // These nine are each deliberately reachable without a token: the SPA root, three health probes, and
    // the five authentication entry points, which cannot require the credential they exist to issue.
    // **A tenth has to be written down here by whoever adds it**, which is the only point at which a
    // decision to open a route to the public is forced through a person.
    //
    // `?` is not a wildcard: the health endpoints carry no `HttpMethodMetadata`, so they answer any verb.
    string[] expectedAnonymous =
    [
      "? /health",
      "? /health/live",
      "? /health/ready",
      "GET /",
      "POST /api/platform/auth/login",
      "POST /api/platform/auth/refresh",
      "POST /api/platform/auth/select-tenant",
      "POST /api/platform/support/auth/login",
      "POST /api/platform/support/auth/refresh"
    ];

    Assert.Equal(
      expectedAnonymous,
      anonymous.Select(Describe).OrderBy(line => line, StringComparer.Ordinal));

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

  // ---- ONE ENUMERATION, READ BY BOTH GUARDS.
  //
  // The join asks *does this name exist?* and the scope guard asks *can the caller this route is for ever
  // hold it?* Two enumerations would be two things that can drift, which is the shape this pair of guards
  // exists to remove — so the plane travels with the name from the single place that recovers it.
  //
  // `DeclaredScope` is the scope the POLICY PREFIX declares, not one read from the catalog: the prefix is
  // how the plane is chosen (`PermissionAuthorizationPolicyProvider.cs:14,19` dispatches on exactly this),
  // so it is the endpoint author's stated intent and the thing the catalog is then checked against.
  private (string Route, PermissionScope DeclaredScope, string Permission)[] RequiredPermissions() =>
  [
    .. RouteEndpoints()
      .SelectMany(endpoint => endpoint.Metadata
        .GetOrderedMetadata<IAuthorizeData>()
        .Select(data => data.Policy)
        .Where(policy => !string.IsNullOrEmpty(policy))
        .Select(policy => (Route: Describe(endpoint), Policy: policy!)))
      .Select(entry => (
        entry.Route,
        Recovered: TryRecoverPermission(entry.Policy, out var permission, out var scope)
          ? (Scope: scope, Permission: permission)
          : ((PermissionScope Scope, string Permission)?)null))
      .Where(entry => entry.Recovered is not null)
      .Select(entry => (entry.Route, entry.Recovered!.Value.Scope, entry.Recovered!.Value.Permission))
      .Distinct()
  ];

  // BOTH PLANES. A platform-support permission is catalog-defined exactly as a tenant one is, so a route on
  // either plane requiring an undefined name is the same defect and is caught by the same pass — and the
  // prefix that resolved it is the plane that route was mapped on.
  private static bool TryRecoverPermission(string policy, out string permission, out PermissionScope scope)
  {
    if (PermissionAuthorizationDefaults.TryGetPermissionName(policy, out permission))
    {
      scope = PermissionScope.Tenant;
      return true;
    }

    if (PlatformPermissionAuthorizationDefaults.TryGetPermissionName(policy, out permission))
    {
      scope = PermissionScope.PlatformSupport;
      return true;
    }

    scope = default;
    return false;
  }

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
