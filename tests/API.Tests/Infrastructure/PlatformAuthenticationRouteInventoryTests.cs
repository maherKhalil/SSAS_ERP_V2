using Microsoft.AspNetCore.Routing;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE TENANT AUTHENTICATION ROUTE SURFACE — `AC-AUTH-0036`'s FIRST CLAUSE, WHICH HAD NO WITNESS.
// ==================================================================================================
//
// *"**EXACTLY** the four approved `/api/platform/auth/*` routes accept only their approved inputs, bind
// `ssas-erp-web` server-side, and return the exact status and Problem Details mappings without cause
// disclosure."*
//
// ---- ⚠⚠ THE TEST THAT LOOKED LIKE THIS ONE IS ABOUT A DOCUMENT, NOT A ROUTE TABLE.
//
// `HostEndpointTests.OpenApi_exposes_only_the_approved_platform_authentication_routes` reads
// `/swagger/v1/swagger.json`. **A swagger path is a DECLARATION; a mapped endpoint is the REALISATION**,
// and a fifth route added without a swagger annotation is invisible to it in exactly the direction that
// matters. That test is the witness for `AC-AUTH-0050`, which is explicitly about OpenAPI. It is not a
// witness for this clause, and its name — containing *only* and *approved* — is what makes the confusion
// cheap.
//
// ---- ⚠⚠⚠ AND ITS `only` WAS A PRESENCE LIST.
//
// Four `TryGetProperty` assertions plus `DoesNotContain(paths, startsWith "/api/auth/")` — **a fifth route
// under `/api/platform/auth/` passes every line of it.** *EXACTLY*, *ONLY* and *NO OTHER* are claims about
// the COMPLEMENT, and a presence list cannot carry one: four asserted present is satisfied by five
// existing. The arity pin is the whole content of the word, and it was the missing assertion in both
// places. (Added there too, over the swagger paths, for `AC-AUTH-0050`.)
//
// ---- WHY THE ANONYMITY SPLIT IS ASSERTED HERE RATHER THAN LEFT TO THE POLICY TESTS.
//
// Three of the four are `AllowAnonymous` and `logout` is not, and **both directions of that mistake are
// serious and silent**: an authenticated `login` is unreachable, and an anonymous `logout` revokes a
// session named by an unvalidated caller. `AuthorizationOf` reports PRESENCE separately from the policy
// string precisely because `Policy` is null for both "no gate" and "gate with no named policy", so the
// split is expressible here and nowhere cheaper.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class PlatformAuthenticationRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/platform/auth";

  private static readonly (string Method, string Pattern, bool RequiresAuthorization)[] Expected =
  [
    ("POST", "/api/platform/auth/login", false),
    ("POST", "/api/platform/auth/logout", true),
    ("POST", "/api/platform/auth/refresh", false),
    ("POST", "/api/platform/auth/select-tenant", false)
  ];

  [Fact]
  [Trait("Criterion", "AC-AUTH-0036")]
  public void The_tenant_authentication_route_surface_is_exactly_the_four_approved_routes()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(route => (Method: PlatformRouteInventory.FirstMethodOf(route), Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    // The floor is not decoration: `Under` filters by prefix, and a renamed prefix returns an empty array
    // from a host that mapped everything correctly — which would read as "exactly the expected four" if the
    // expectation were also empty. Here the expectation is non-empty so the equality below already fails,
    // but the floor names the cause instead of printing two lists.
    Assert.NotEmpty(actual);

    var expected = Expected
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  // ---- THE SPLIT, ASSERTED PER ROUTE SO A FAILURE NAMES THE ROUTE AND THE DIRECTION.
  //
  // ⚠ A single `Assert.Equal(3, anonymous.Count)` would hold if `login` and `logout` swapped states. The
  // property is per-route and the assertion is too.
  [Fact]
  [Trait("Criterion", "AC-AUTH-0036")]
  public void Only_logout_requires_authorization_and_the_other_three_are_anonymous()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix).ToDictionary(
      route => $"{PlatformRouteInventory.FirstMethodOf(route)} {route.RoutePattern.RawText}",
      PlatformRouteInventory.AuthorizationOf,
      StringComparer.Ordinal);

    foreach (var (method, pattern, requiresAuthorization) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");
      Assert.Equal(requiresAuthorization, actual[key].HasAuthorization);
    }
  }
}
