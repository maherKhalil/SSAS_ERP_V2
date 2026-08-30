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
  // ================================================================================================
  // ⚠ WHAT THIS INVENTORY CANNOT SEE, AND IT LET FIVE WRONG ROWS STAND FOR MONTHS (T-136).
  // ================================================================================================
  //
  // **This file compares code to code.** It pins all 21 routes and their permissions against the running
  // endpoint table, and it has been green throughout — **while `FP-011`'s `api-contracts.md` documented
  // three of them under `GL.Accounts.Manage`, a permission that has never existed**, named
  // `GL.Periods.Manage` where closing carries `GL.Periods.Close`, listed `POST /api/gl/journals` which is
  // not a route, and omitted three that are.
  //
  // **An inventory would have caught the omissions if the specification were its other side. It is not.**
  // The comparison is against the ENDPOINT TABLE, so a specification saying something different is invisible
  // to it — **and `DEC-L-002` is why: nothing mechanical reads prose.**
  //
  // **So the green here is not evidence the documentation is right, and it never was.** Stated because an
  // inventory's presence reads as though the surface is fully accounted for, and half of "accounted for"
  // lives in a document no test opens.

  private static readonly (string Method, string Pattern, string Permission)[] Expected =
  [
    ("POST", "/api/gl/accounts", GlPermissionNames.CreateAccounts),
    ("GET", "/api/gl/accounts", GlPermissionNames.ViewAccounts),
    ("GET", "/api/gl/accounts/{accountId}", GlPermissionNames.ViewAccounts),
    ("PUT", "/api/gl/accounts/{accountId}", GlPermissionNames.UpdateAccounts),
    ("POST", "/api/gl/accounts/{accountId}/deactivation", GlPermissionNames.DeactivateAccounts),
    ("POST", "/api/gl/accounts/{accountId}/activation", GlPermissionNames.DeactivateAccounts),
    ("GET", "/api/gl/accounts/{accountId}/balance", GlPermissionNames.ViewReports),

    ("POST", "/api/gl/fiscal-years", GlPermissionNames.ManagePeriods),
    ("GET", "/api/gl/fiscal-periods", GlPermissionNames.ViewPeriods),
    ("POST", "/api/gl/fiscal-periods/{fiscalPeriodId}/closure", GlPermissionNames.ClosePeriods),
    ("POST", "/api/gl/fiscal-periods/{fiscalPeriodId}/reopening", GlPermissionNames.ClosePeriods),

    // ---- THE TWO READS, ADDED IN T-098. THE HALF FP-011 NEVER BUILT.
    //
    // Create, update, discard and post shipped and **nothing could read a draft** — the create route
    // returns a `Location` header and an id for a resource no route could fetch.
    //
    // `GL.Drafts.View` and nothing else: neither `ManageDrafts` above nor `PostJournals` below grants it.
    // **This inventory is where that pairing is visible at a glance**, which is why the permission column
    // exists — a reader can see that the four writes and the two reads are gated differently on purpose.
    ("GET", "/api/gl/journal-drafts", GlPermissionNames.ViewDrafts),
    ("GET", "/api/gl/journal-drafts/{journalDraftId}", GlPermissionNames.ViewDrafts),
    ("POST", "/api/gl/journal-drafts", GlPermissionNames.ManageDrafts),
    ("PUT", "/api/gl/journal-drafts/{journalDraftId}", GlPermissionNames.ManageDrafts),
    ("POST", "/api/gl/journal-drafts/{journalDraftId}/discard", GlPermissionNames.ManageDrafts),
    ("POST", "/api/gl/journal-drafts/{journalDraftId}/posting", GlPermissionNames.PostJournals),

    ("GET", "/api/gl/journals", GlPermissionNames.ViewJournals),
    ("GET", "/api/gl/journals/{journalEntryId}", GlPermissionNames.ViewJournals),
    ("POST", "/api/gl/journals/{journalEntryId}/reversals", GlPermissionNames.ReverseJournals),

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

  // ---- A POSTED JOURNAL HAS NO MUTATION ROUTE, AND THAT ABSENCE IS WHAT ENFORCES `BR-GL-0002` (T-166).
  //
  // ⚠ **THIS TEST IS NOW THE ONLY THING HOLDING `BR-GL-0002` AT THE TRANSPORT BOUNDARY (T-170).**
  //
  // `JournalErrors.Immutable` was declared, mapped to 409, and returned by nothing. T-166 kept it on the
  // argument that it was the rule's only trace in source. **That premise was false** — `BR-GL-0002` is
  // named in thirteen places across eight files — so the code was removed in T-170 as a door with no room
  // behind it, on the same reading that retired Payroll's `PayElementCodeImmutable`.
  //
  // **A removal can need a test, and the test is for what the removal left holding the rule.** Here that is
  // the absence of a mutation route:
  //
  // ```
  // no PUT / PATCH / DELETE on /journals/{journalEntryId}   a caller cannot ask to mutate a posted journal
  // drafts.Remove(draft) in the SAME transaction as posting  there is no posted draft left to mutate;
  //                                                          PUT /journal-drafts/{id} answers DraftNotFound
  // ```
  //
  // **`DEC-L-084`'s shape: an invariant enforced structurally rather than by a check.** Deleting the error
  // because "nothing returns it" would erase the only trace of `BR-GL-0002` from the source, and
  // `api-contracts.md` documents it as promised behaviour.
  //
  // ⚠ **THIS IS THE TEST THAT MAKES THE ABSENCE LOAD-BEARING.** Someone adding `PUT /journals/{id}` later
  // would find a named 409 already declared and mapped, and could reasonably assume it is live. It is not,
  // and nothing else would tell them.
  //
  // **`POST /journals/{id}/reversals` is the one permitted write and is named here**, because a rule that
  // said "no writes under /journals" would be false and would be deleted the first time it fired.
  [Fact]
  [Trait("Decision", "BR-GL-0002")]
  public void A_posted_journal_exposes_no_mutation_route()
  {
    var underAJournal = host.MappedRoutes()
      .Where(route => route.Pattern.Contains("/journals/{journalEntryId}", StringComparison.Ordinal))
      .ToArray();

    // Without this the two assertions below pass against an empty set (`DEC-L-070`).
    Assert.NotEmpty(underAJournal);

    var mutating = underAJournal
      .Where(route => route.Method is "PUT" or "PATCH" or "DELETE")
      .Select(route => $"{route.Method} {route.Pattern}")
      .ToArray();

    Assert.Empty(mutating);

    // The permitted write, named rather than implied.
    var posts = underAJournal
      .Where(route => route.Method == "POST")
      .Select(route => route.Pattern)
      .ToArray();

    Assert.All(posts, pattern =>
      Assert.EndsWith("/journals/{journalEntryId}/reversals", pattern, StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "OD-GL-0007")]
  public void Posting_lives_under_the_draft_because_the_journal_does_not_exist_yet()
  {
    // Placing it under /journals would suggest a journal exists before it does. The route shape is the
    // two-aggregate ruling made visible in the URL.
    Assert.Contains(
      host.MappedRoutes(),
      route => route.Pattern.EndsWith("/journal-drafts/{journalDraftId}/posting", StringComparison.Ordinal));

    Assert.DoesNotContain(host.MappedRoutes(), route => route.Pattern.EndsWith("/journals/posting", StringComparison.Ordinal));
  }
}
