using Microsoft.AspNetCore.Routing;
using SSAS.API.Tests.Infrastructure;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Companies;

// ==================================================================================================
// PLATFORM COMPANIES' ROUTE INVENTORY (T-129).
// ==================================================================================================
//
// Second of Platform's inventories, after Localization's in T-128. **Seven routes, three permissions, and
// the split between them is the property worth pinning:** reading, editing and changing lifecycle state are
// three separate grants, and `Lifecycle` is the one that can archive a company out from under its users.
//
// ---- ⚠ THE EXPECTATION WAS READ OFF THE RUNNING SURFACE, SO THE GREEN IS AN ARTEFACT.
//
// `DEC-L-070`, and it applies to each inventory separately — **T-114's lesson that four paths needed four
// plants, and knowing why the first passed did not transfer to the second.** This one was planted on its
// own: `Companies.View` substituted for `Companies.Lifecycle` on `archive`, which failed.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class CompanyRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/platform/companies";

  // ⚠ THE TRAILING SLASH ON THE COLLECTION ROWS IS REAL. `MapGroup(prefix).MapGet("")` yields a `RawText`
  // of `/api/platform/companies/`; routing matches the slash-less URL, which is what the fifteen existing
  // `CompaniesEndpointTests` calls send. **These rows record the pattern, not the caller's URL.**
  private static readonly (string Method, string Pattern, string Policy)[] Expected =
  [
    ("GET", "/api/platform/companies/",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ViewCompanies),
    ("POST", "/api/platform/companies/",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ManageCompanies),
    ("GET", "/api/platform/companies/{companyId}",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ViewCompanies),
    ("PUT", "/api/platform/companies/{companyId}",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ManageCompanies),

    // ---- LIFECYCLE. Separate from `Manage` because these three change whether the company can be USED,
    // ---- not what it says. A user who may correct a company's name may not archive it.
    ("POST", "/api/platform/companies/{companyId}/activate",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.CompanyLifecycle),
    ("POST", "/api/platform/companies/{companyId}/archive",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.CompanyLifecycle),
    ("POST", "/api/platform/companies/{companyId}/deactivate",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.CompanyLifecycle)
  ];

  [Fact]
  public void The_company_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(route => (Method: PlatformRouteInventory.FirstMethodOf(route), Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    // NOT VACUOUS — a renamed mount point would empty both sides of the comparison.
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
    // The property the set comparison cannot see: a route present, correctly named, and gated on the WRONG
    // permission. `ViewCompanies` on `archive` would satisfy every assertion above while letting any reader
    // archive a company.
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

  // ================================================================================================
  // ⚠ AN OBSERVATION THIS INVENTORY RECORDS AND DOES NOT ACT ON.
  // ================================================================================================
  //
  // **`{companyId}` carries no route constraint**, where `{tenantUserId:long}` and `{principalId:long}` in
  // the sibling Platform groups do, and Attendance's inventory is `:guid` throughout. **So a non-GUID
  // company id reaches the handler rather than failing to match**, and what the caller receives depends on
  // how the handler parses it rather than on routing.
  //
  // **Not changed here.** Adding a constraint moves a 400 to a 404 for malformed ids, which is a transport
  // behaviour change and belongs to a task that says so. **Recorded so it is a decision when someone makes
  // it, rather than a difference nobody noticed** — this file's job is to pin the surface, and the surface
  // includes the absence.
  [Fact]
  public void The_company_id_segment_is_unconstrained_and_that_is_pinned_rather_than_asserted_correct()
  {
    var parameterised = PlatformRouteInventory.Under(factory, RoutePrefix)
      .Select(route => route.RoutePattern.RawText!)
      .Where(pattern => pattern.Contains("{companyId", StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(parameterised);
    Assert.All(parameterised, pattern => Assert.DoesNotContain("{companyId:", pattern, StringComparison.Ordinal));
  }
}
