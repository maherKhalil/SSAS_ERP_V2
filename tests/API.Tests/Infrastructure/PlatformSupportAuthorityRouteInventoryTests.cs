using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// PLATFORM SUPPORT AUTHORITY'S ROUTE INVENTORY (T-129) — **AND ITS PERMISSION PROPERTY IS NEARLY VACUOUS.**
// ==================================================================================================
//
// Nine routes, **all nine carrying `Platform.Support.Administer`.** This is the support plane: registering
// support principals, granting and revoking their authority, disabling and re-enabling them.
//
// ---- ⚠ READ THIS BEFORE TRUSTING THE PERMISSION TEST BELOW.
//
// **A mis-gated route is not expressible here.** In Attendance the guard earns its place because
// `ViewLeave` and `ApproveLeave` are both real permissions of the same group, so a route can be gated on
// the *wrong one of ours* — and T-099's plant demonstrated exactly that. **This group has one permission.
// There is no wrong one to pick, so that failure mode does not exist and the property is green by
// construction.**
//
// **Stated precisely rather than as a blanket "vacuous", because the test is not useless:** it still
// catches a gate REMOVED, and a gate replaced by a FOREIGN policy — a tenant-plane `Permission:` prefix
// where the platform-plane `PlatformPermission:` belongs would fire, and that is a real and serious
// mistake on this surface (`ADR-015`'s plane separation). **What it cannot catch is a mix-up among the
// group's own permissions, because there is only one.**
//
// **Said here because a vacuous property that does not say so is a floor stated too narrowly** — the shape
// that produced four separate findings across T-120, T-125, T-126 and T-128.
//
// ---- THE EXPECTATION WAS READ OFF THE RUNNING SURFACE.
//
// Planted on its own: `revoke` swapped to the tenant-plane prefix, which failed. `DEC-L-070` per inventory.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class PlatformSupportAuthorityRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/platform/support/principals";

  // ---- THE PLANE PREFIX IS THE PART THAT VARIES, AND IT IS THE PART WORTH PINNING.
  private const string SupportPolicy =
    PermissionPolicyNames.PlatformPrefix + PlatformPermissionNames.AdministerPlatformSupport;

  private static readonly (string Method, string Pattern)[] Expected =
  [
    ("GET", "/api/platform/support/principals/"),
    ("POST", "/api/platform/support/principals/"),
    ("GET", "/api/platform/support/principals/{principalId}"),
    ("GET", "/api/platform/support/principals/{principalId}/assignments"),
    ("GET", "/api/platform/support/principals/{principalId}/permissions"),

    // ---- THE FOUR AUTHORITY TRANSITIONS. Granting, revoking, disabling and re-enabling a support
    // ---- principal all sit behind the single administer permission — see the vacuity note above.
    ("POST", "/api/platform/support/principals/{principalId}/disable"),
    ("POST", "/api/platform/support/principals/{principalId}/grant"),
    ("POST", "/api/platform/support/principals/{principalId}/reenable"),
    ("POST", "/api/platform/support/principals/{principalId}/revoke")
  ];

  [Fact]
  public void The_support_authority_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(route => (Method: PlatformRouteInventory.FirstMethodOf(route), Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.NotEmpty(actual);

    var expected = Expected
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void Every_route_requires_the_platform_plane_support_permission()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix).ToDictionary(
      route => $"{PlatformRouteInventory.FirstMethodOf(route)} {route.RoutePattern.RawText}",
      PlatformRouteInventory.AuthorizationOf,
      StringComparer.Ordinal);

    foreach (var (method, pattern) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");
      Assert.True(actual[key].HasAuthorization, $"{key} carries no authorization metadata at all");
      Assert.Equal(SupportPolicy, actual[key].Policy);
    }
  }

  // ---- THE PROPERTY THAT IS NOT VACUOUS, ASSERTED ON ITS OWN SO A FAILURE NAMES IT.
  //
  // **No route on this surface may carry a TENANT-plane policy.** `ADR-015` separates the two planes, and a
  // support route gated on `Permission:` rather than `PlatformPermission:` would be authorised by a tenant
  // user's grants — which is the one mistake this group's uniform permission still permits.
  [Fact]
  public void No_support_route_is_gated_on_a_tenant_plane_policy() =>
    Assert.Empty(PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(PlatformRouteInventory.AuthorizationOf)
      .Where(authorization => authorization.Policy?.StartsWith(
        PermissionPolicyNames.TenantPrefix, StringComparison.Ordinal) ?? false));
}
