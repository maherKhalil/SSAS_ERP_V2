using SSAS.API.Tests.Infrastructure;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Tenants;

// ==================================================================================================
// THE TENANT REGISTRY'S ROUTE SURFACE, PINNED (T-155).
// ==================================================================================================
//
// Seven handlers gained seven routes. **The inventory is the guard that says which seven** — a route added
// here later without a line in this list fails, and so does a line with no route behind it.
//
// ---- ⚠ AND THE PERMISSION ON EACH ROUTE IS THE HALF THAT MATTERS MOST.
//
// These routes create, suspend and archive TENANTS. A wrong policy string here is not a bug in a feature;
// it is one tenant's administrator reaching another tenant's existence. The second test asserts the exact
// policy per route rather than merely that authorization metadata is present.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/platform/tenants";

  // ---- `AdministerTenant` IS DELIBERATELY ABSENT, AND ITS ABSENCE IS THE POINT.
  //
  // `Platform.Tenant.Administer` reads like a fourth tenant permission. It is authority WITHIN a tenant
  // ("reach every active branch", used by `TenantAdministratorAuthority`), and **granting it on any route
  // below would let a tenant administrator archive other tenants.** If it ever appears in this list,
  // that is the change to challenge.
  private static readonly (string Method, string Pattern, string Policy)[] Expected =
  [
    ("POST", "/api/platform/tenants/",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.ManageTenants),
    ("GET", "/api/platform/tenants/",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.ViewTenants),
    ("GET", "/api/platform/tenants/{tenantId}",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.ViewTenants),
    ("POST", "/api/platform/tenants/{tenantId}/activate",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.TenantLifecycle),
    ("POST", "/api/platform/tenants/{tenantId}/archive",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.TenantLifecycle),
    ("POST", "/api/platform/tenants/{tenantId}/reactivate",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.TenantLifecycle),
    ("POST", "/api/platform/tenants/{tenantId}/suspend",
      PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.TenantLifecycle)
  ];

  [Fact]
  public void The_tenant_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(route => (
        Method: PlatformRouteInventory.FirstMethodOf(route),
        Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    // Without this the equality below passes against an empty surface the day the Map call is dropped
    // from `Program.cs` — the failure this whole file exists to catch (`DEC-L-070`).
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
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix).ToDictionary(
      route => $"{PlatformRouteInventory.FirstMethodOf(route)} {route.RoutePattern.RawText}",
      PlatformRouteInventory.AuthorizationOf,
      StringComparer.Ordinal);

    foreach (var (method, pattern, policy) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");
      Assert.True(actual[key].HasAuthorization, $"{key} carries no authorization metadata at all");
      Assert.Equal(policy, actual[key].Policy);
    }
  }

  // ---- NO ROUTE IN THIS FAMILY IS ANONYMOUS, ASSERTED SEPARATELY FROM THE POLICY STRINGS.
  //
  // The test above walks the EXPECTED list, so a route that exists with no authorization at all — one
  // nobody wrote a line for — would never be visited by it. This walks the ACTUAL surface instead.
  [Fact]
  public void No_tenant_route_is_reachable_without_authorization()
  {
    var unprotected = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Where(route => !PlatformRouteInventory.AuthorizationOf(route).HasAuthorization)
      .Select(route => $"{PlatformRouteInventory.FirstMethodOf(route)} {route.RoutePattern.RawText}")
      .ToArray();

    Assert.Empty(unprotected);
  }
}
