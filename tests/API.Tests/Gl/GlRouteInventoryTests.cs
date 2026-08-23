using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.GL.Application.Permissions;

namespace SSAS.API.Tests.Gl;

[Collection(GlApiEndpointGroup.Name)]
public sealed class GlRouteInventoryTests(GlApiTestHost host) : IClassFixture<GlApiTestHost>
{
  // ---- THE INVENTORY IS PINNED BY NAME, NOT BY COUNT.
  //
  // A count alone passes when one route is removed and another added. Naming every route means a change to
  // the surface has to be acknowledged here, which is where a reviewer will see it — and `api-contracts.md`
  // is the document it has to agree with.
  private static readonly (string Method, string Pattern, string Permission)[] Expected =
  [
    ("POST", "/api/gl/accounts", GlPermissionNames.CreateAccounts),
    ("GET", "/api/gl/accounts", GlPermissionNames.ViewAccounts),
    ("GET", "/api/gl/accounts/{accountId:guid}", GlPermissionNames.ViewAccounts),
    ("PUT", "/api/gl/accounts/{accountId:guid}", GlPermissionNames.UpdateAccounts),
    ("POST", "/api/gl/accounts/{accountId:guid}/deactivation", GlPermissionNames.DeactivateAccounts),
    ("POST", "/api/gl/accounts/{accountId:guid}/activation", GlPermissionNames.DeactivateAccounts),
    ("GET", "/api/gl/accounts/{accountId:guid}/balance", GlPermissionNames.ViewReports),

    ("POST", "/api/gl/fiscal-years", GlPermissionNames.ManagePeriods),
    ("GET", "/api/gl/fiscal-periods", GlPermissionNames.ViewPeriods),
    ("POST", "/api/gl/fiscal-periods/{fiscalPeriodId:guid}/closure", GlPermissionNames.ClosePeriods),
    ("POST", "/api/gl/fiscal-periods/{fiscalPeriodId:guid}/reopening", GlPermissionNames.ClosePeriods),

    ("POST", "/api/gl/journal-drafts", GlPermissionNames.ManageDrafts),
    ("PUT", "/api/gl/journal-drafts/{journalDraftId:guid}", GlPermissionNames.ManageDrafts),
    ("POST", "/api/gl/journal-drafts/{journalDraftId:guid}/discard", GlPermissionNames.ManageDrafts),
    ("POST", "/api/gl/journal-drafts/{journalDraftId:guid}/posting", GlPermissionNames.PostJournals),

    ("GET", "/api/gl/journals", GlPermissionNames.ViewJournals),
    ("GET", "/api/gl/journals/{journalEntryId:guid}", GlPermissionNames.ViewJournals),
    ("POST", "/api/gl/journals/{journalEntryId:guid}/reversals", GlPermissionNames.ReverseJournals),

    ("GET", "/api/gl/reports/trial-balance", GlPermissionNames.ViewReports)
  ];

  [Fact]
  public void The_gl_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = host.MappedRoutes()
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    var expected = Expected
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void Every_gl_route_requires_a_permission()
  {
    // A route without a policy is reachable by any authenticated caller. That is the single worst mistake
    // this surface could make, and it is invisible in a diff that simply forgets one line.
    var unprotected = host.MappedRoutes()
      .Where(route => string.IsNullOrEmpty(route.Policy))
      .Select(route => $"{route.Method} {route.Pattern}")
      .ToArray();

    Assert.Empty(unprotected);
  }

  [Fact]
  public void Every_route_requires_the_permission_the_inventory_names()
  {
    var actual = host.MappedRoutes()
      .ToDictionary(route => $"{route.Method} {route.Pattern}", route => route.Policy, StringComparer.Ordinal);

    foreach (var (method, pattern, permission) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");
      Assert.Equal($"{PermissionPolicyNames.TenantPrefix}{permission}", actual[key]);
    }
  }

  [Fact]
  [Trait("Decision", "api-contracts.md")]
  public void No_gl_route_responds_to_delete()
  {
    // ---- THE ABSENCE IS THE ASSERTION.
    //
    // The one destructive operation in this module is POST /journal-drafts/{id}/discard, and a draft was
    // never part of the ledger. Nothing responds to DELETE, so no client can assume anything does — and a
    // future route that added the verb would have to delete this test to pass.
    var deleteRoutes = host.MappedRoutes()
      .Where(route => string.Equals(route.Method, "DELETE", StringComparison.OrdinalIgnoreCase))
      .Select(route => route.Pattern)
      .ToArray();

    Assert.Empty(deleteRoutes);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0007")]
  public void Posting_lives_under_the_draft_because_the_journal_does_not_exist_yet()
  {
    // Placing it under /journals would suggest a journal exists before it does. The route shape is the
    // two-aggregate ruling made visible in the URL.
    Assert.Contains(
      host.MappedRoutes(),
      route => route.Pattern.EndsWith("/journal-drafts/{journalDraftId:guid}/posting", StringComparison.Ordinal));

    Assert.DoesNotContain(host.MappedRoutes(), route => route.Pattern.EndsWith("/journals/posting", StringComparison.Ordinal));
  }
}
