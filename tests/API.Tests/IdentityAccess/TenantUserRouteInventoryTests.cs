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
// ---- ⚠⚠⚠ CORRECTION: *ARE TESTED* IS TRUE OF TWO OF THOSE FIVE, NOT OF ALL FIVE.
//
// Measured by CONSTRUCTION SITE across `tests/` — the completable search, since a handler must be named to
// be built:
//
//   `CreateTenantUserMembershipCommandHandler`   constructed in `IdentityAccessApplicationTests`      ✓
//   `SetTenantUserBranchesCommandHandler`        constructed in `TenantBranchLifecycleSqlServerTests`  ✓
//   `ListTenantUsersQueryHandler`                NO TEST CONSTRUCTS IT
//   `GetTenantUserByIdQueryHandler`              NO TEST CONSTRUCTS IT
//   `UpdateTenantUserProfileCommandHandler`      NO TEST CONSTRUCTS IT
//
// ⚠ THE WORDING WAS *NO TEST NAMES IT* AND A RESIDUAL AUDIT MADE IT FALSE — because THIS COMMENT NAMES
// ALL THREE, and so does `PlatformReadScopeArchitectureTests`. **Writing the absence down is what
// falsified it.** The claim was always about CONSTRUCTION, so it now says so.
//
// **That is the mirror problem at the level of PROSE rather than of an instrument**: the same shape as a
// citation guard resolving against text that includes its own comments, or an allow-list whose entries
// satisfy the search that reads it. ⚠⚠ **AN ABSENCE CLAIM STATED IN THE SEARCH SPACE IT QUANTIFIES OVER
// IS SELF-REFUTING** — and a `grep` for the name is exactly how a future reader would check it, which is
// how they would meet the false version.
//
// **So three of the five are neither routed NOR constructed by any test.** *Exists* is the part that
// holds; *is tested* was the part nobody checked, and it is the sentence that makes the gap look safe.
//
// ⚠⚠ AND THAT MATTERS BEYOND THE INVENTORY: `AC-IAM-0001` — *"A tenant administrator sees only users from
// the current tenant"* — IS UNCOVERED. Its subject is the listing, the listing has no route, and
// `ListTenantUsersQueryHandler` is on no executed path. **The tenant-scoping of that query is asserted by
// nothing.**
//
// ⚠ This repository's own prior applies: the two read services no test ever constructed were the two
// carrying live defects, one of them a financial report that threw on every call.
//
// ---- ⚠⚠⚠ SO THEY WERE READ. THEY ARE CORRECT — AND *WHY* THEY ARE CORRECT IS THE FINDING.
//
// `ListTenantUsersQueryHandler` validates a tenant actor and then calls
// `readService.ListAsync(pageNumber, pageSize, ct)`. **IT PASSES NO TENANT ID.** `TenantUserReadService`
// then queries `dbContext.TenantUsers` **WITH NO TENANT PREDICATE** — and `GetByIdAsync` matches on
// `item.Id` alone. **Nothing in either file scopes anything to a tenant.**
//
// The scoping is real and lives two layers away: `PersistenceDbContext.ConfigureTenantFilter<TEntity>`
// applies a global query filter to every `ITenantOwnedEntity` —
// `CurrentTenantId.HasValue && entity.TenantId == CurrentTenantId.Value` — which also FAILS CLOSED, since
// no ambient tenant yields no rows rather than all rows.
//
// **So `AC-IAM-0001` is ENFORCED and UNOBSERVED, and the enforcement is invisible at both sites that
// depend on it.** That is not a defect; it is the intended ADR-005 mechanism. What it means for a reader:
//
// ⚠⚠ **ADDING `IgnoreQueryFilters()` TO EITHER METHOD WOULD LEAK EVERY TENANT'S USERS, AND IT IS A
// NORMAL-LOOKING EDIT** — `AccessTokenClaimsProvider` and `TenantAdministratorAuthority` both call it
// legitimately, with an explicit `TenantId ==` predicate supplied by hand instead. **A developer copying
// that idiom into `TenantUserReadService` and forgetting the hand-written predicate removes tenant
// isolation from the user list, and no test anywhere would fail.**
//
// ⚠ The outcome is therefore none of *broken*, *fine and tested*, or *dead*: **correct, load-bearing on a
// mechanism named nowhere near it, and undefended.**
//
// ---- ⚠⚠ IT IS NO LONGER UNDEFENDED, AND `AC-IAM-0001` IS STILL UNCOVERED. BOTH ARE TRUE.
//
// `PlatformReadScopeArchitectureTests` now requires every Platform read service that ignores the global
// tenant filter to supply its own `TenantId ==` predicate or declare itself cross-tenant, and floors the
// count that relies on the filter. **Planted: `IgnoreQueryFilters()` in `TenantUserReadService.ListAsync`
// reddens both of its tests.** So the edit that would leak the user list is now caught.
//
// **THAT GUARD IS NOT CITED FOR `AC-IAM-0001` AND MUST NOT BE.** The criterion says the listing RETURNS
// ONLY ONE TENANT'S ROWS; the guard asserts a predicate is present and a call is absent, and never
// executes a query. **A structural guarantee reads as behavioural coverage and is not it** — this
// repository's own instance is an architecture test that passed over two read services which could not
// run at all.
//
// The link is recorded at both ends so a reader finds the other; the criterion keeps its honest status.
// A behavioural witness still needs the query filter and a real context — `Integration.Tests`, outside
// this loop's gate.
//
// **That is recorded here rather than fixed**: building the read surface is a feature. The inventory's job
// is to make the gap legible to whoever picks it up, and a list of four POSTs with no GET is the clearest
// statement of it that exists anywhere in the tests.
//
// ==================================================================================================
// ⚠⚠⚠ THE CLASS THIS FILE IS TWO INSTANCES OF: **A CRITERION'S SUBJECT EXISTS, IS CORRECT, AND IS ON NO
// EXECUTED PATH.**
// ==================================================================================================
//
// Three instances found on 2026-09-02/03, across two packages. Collected here because **three instances of
// one cause is a property of the codebase; three findings is a list of complaints** — and because the
// three are NOT equivalent, which is the part a list would lose.
//
//   1. DEFENDED-BUT-UNWITNESSED — `AC-IAM-0001`, above. The tenant-scoping of the user listing is real,
//      enforced two layers away by `ConfigureTenantFilter`, and now structurally guarded. **No test ever
//      observes the behaviour**, because there is no route and the handler is on no executed path.
//
//   2. UNROUTED-AND-UNTESTED — the three handlers above. `ListTenantUsers`, `GetTenantUserById`,
//      `UpdateTenantUserProfile`: named in no `SSAS.Platform.API` file and constructed by no test.
//      Searched by CONSTRUCTION SITE, the completable search, since a handler must be named to be built.
//
//   3. UNREACHABLE-AND-DOCUMENTED-AS-REDUNDANT — `AC-SUB-0016`, in
//      `Platform.Tests/Subscriptions/SubscriptionInvariantTests.cs`. `TenantEntitlementGrant`'s two
//      factories have no caller in `src/`; the trial seed writes five other tables; the creating migration
//      asserts the table is EMPTY. Searched by TABLE NAME as well as type name, because a raw `INSERT`
//      names no C# symbol.
//
// ⚠ **THE THIRD IS THE WORST, AND NOT BECAUSE IT IS THE MOST BROKEN — IT IS THE LEAST BROKEN.** In 1 and 2
// the gap is legible: no route, no test, nothing claims otherwise. In 3 the source **documents the
// unreachable guard as one half of a deliberate redundant pair**, so the reader most likely to touch it is
// one tidying away a duplication that is not one. **The redundancy claim is false in exactly the direction
// that makes removal look safe.**
//
// ⚠⚠ AND ALL THREE ARE *ARMED* RATHER THAN *CURRENT*. Nothing is broken today in any of them: the exposure
// in each case arrives when the missing execution path is built, which is also the moment nobody is
// looking at the guard. **A defect that requires future correct-looking work to become live is invisible
// to every instrument this repository has**, because every one of them observes what exists.
//
// The benign reading is stated at each site and is probably the true one in all three: a domain and schema
// built ahead of an application layer is ordinary. **The class is about what the documentation claims, not
// about anyone's competence.**
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
    ("POST", "/api/platform/tenant-users/{tenantUserId}/deactivation",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.DeactivateUsers),
    ("POST", "/api/platform/tenant-users/{tenantUserId}/reactivation",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.ReactivateUsers),

    // ---- EMPLOYEE LINK. Also two permissions, and for the same reason: unlinking severs a user from the
    // ---- employee record that carries their payroll and attendance identity.
    ("POST", "/api/platform/tenant-users/{tenantUserId}/employee-link",
      PermissionPolicyNames.TenantPrefix + PlatformPermissionNames.LinkEmployees),
    ("POST", "/api/platform/tenant-users/{tenantUserId}/employee-link/remove",
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
