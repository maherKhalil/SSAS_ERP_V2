using Microsoft.AspNetCore.Routing;
using SSAS.API.Tests.Infrastructure;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.IdentityAccess;

// ==================================================================================================
// PLATFORM TENANT USERS' ROUTE INVENTORY (T-129).
// ==================================================================================================
//
// **Four routes, four permissions, and every one of them a POST.**
//
// ---- ⚠ THE SHAPE IS THE FINDING, AND IT IS VISIBLE ONLY WHEN THE ROUTES ARE LISTED TOGETHER.
//
// **There is no read surface for tenant users at all.** No `GET`, no list, no detail — and
// `Platform.Users.View` is one of the sixteen catalogued permissions that no route requires (T-128).
// `Platform.Users.Create` and `Platform.Users.Update` are two more.
//
// **So this group is four state transitions on users that no HTTP caller can enumerate**, and the handlers
// that would enumerate them — `ListTenantUsers`, `GetTenantUserById`, `UpdateTenantUserProfile`,
// `CreateTenantUserMembership`, `SetTenantUserBranches` — **exist, are tested, and are named nowhere in
// `SSAS.Platform.API`.**
//
// **That is recorded here rather than fixed**: building the read surface is a feature. The inventory's job
// is to make the gap legible to whoever picks it up, and a list of four POSTs with no GET is the clearest
// statement of it that exists anywhere in the tests.
//
// ---- THE EXPECTATION WAS READ OFF THE RUNNING SURFACE, SO THE GREEN IS AN ARTEFACT.
//
// Planted on its own — `employee-link/remove` renamed — which failed. `DEC-L-070` per inventory, T-114's
// four-plants lesson.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantUserRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/platform/tenant-users";

  private static readonly (string Method, string Pattern, string Policy)[] Expected =
  [
    // ---- DE/REACTIVATION. Two permissions rather than one: restoring access is a different decision from
    // ---- withdrawing it, and only one of them is reversible by the person who made the mistake.
    ("POST", "/api/platform/tenant-users/{tenantUserId:long}/deactivation",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.DeactivateUsers),
    ("POST", "/api/platform/tenant-users/{tenantUserId:long}/reactivation",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ReactivateUsers),

    // ---- EMPLOYEE LINK. Also two permissions, and for the same reason: unlinking severs a user from the
    // ---- employee record that carries their payroll and attendance identity.
    ("POST", "/api/platform/tenant-users/{tenantUserId:long}/employee-link",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.LinkEmployees),
    ("POST", "/api/platform/tenant-users/{tenantUserId:long}/employee-link/remove",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.UnlinkEmployees)
  ];

  [Fact]
  public void The_tenant_user_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(route => (Method: PlatformRouteInventory.FirstMethodOf(route), Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

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
    // `LinkEmployees` where `UnlinkEmployees` belongs would satisfy the set comparison entirely while
    // letting anyone who may attach an employee also detach one.
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

  // ---- THE ABSENCE, ASSERTED SO IT CANNOT BE FILLED SILENTLY.
  //
  // **A read route appearing here should be a moment somebody notices**, because it is the first half of a
  // transport that does not exist and its arrival changes what the sixteen unrouted permissions mean.
  [Fact]
  public void No_tenant_user_route_answers_a_read() =>
    Assert.Empty(PlatformRouteInventory.Under(factory, RoutePrefix)
      .Where(route => PlatformRouteInventory.FirstMethodOf(route) is "GET" or "HEAD"));
}
