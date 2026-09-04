using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// ⚠⚠⚠ THE POSITIVE HALF OF THE TENANT AUTH SURFACE. IT DID NOT EXIST UNTIL `280`.
// ==================================================================================================
//
// `POST /api/platform/auth/login` had FIVE test references and **every one asserted a REJECTION** — an
// HTTP-not-HTTPS 403, an unknown-field 400, two OpenAPI document reads and a permission-catalogue row.
// `POST /api/platform/auth/select-tenant` had NONE OF ANY KIND. The handlers underneath are well covered
// at the application layer (`AuthenticationSessionApplicationTests`) and against real persistence
// (`PlatformAuthenticationPersistenceTests`) — **it is the HTTP BINDING layer that nothing exercised.**
//
// ⚠ A SUITE THAT ONLY EVER ASSERTS REFUSALS CANNOT DISTINGUISH *the route correctly rejects bad input*
// FROM *the route rejects everything*. All five prior tests stay green if login is broken outright.
//
// ---- WHY THIS IS A GUARD AND NOT COVERAGE, WHICH IS THE WHOLE POINT OF THE ITEM.
//
// These two routes bind through `AuthenticationEndpointRouteBuilderExtensions.ReadJsonAsync<T>`, a PRIVATE
// copy of `StrictRequestReader` that deserializes with `new JsonSerializerOptions(JsonSerializerDefaults.Web)`
// — **case-INsensitive**. `AuthenticationLoginRequest` and `AuthenticationTenantSelectionRequest` therefore
// carry no `[JsonPropertyName]`, and `StrictRequestBindingArchitectureTests` EXCLUDES them for that reason:
// they are unreachable from any `ReadStrictJsonAsync<...>` call site, so they fall outside its closure.
//
// ⚠⚠ THAT EXCLUSION IS CORRECT AND IT IS SILENTLY LOAD-BEARING ON THE `Web` OPTIONS. Convert these readers
// to the shared `StrictRequestReader` — which uses `JsonSerializerOptions.Default`, case-SENSITIVE — and
// camelCase JSON stops binding to PascalCase properties, every field arrives null, and both routes answer
// `400 request.invalid` forever. **That is FP-011 exactly, on the login route.**
//
// ⚠⚠⚠ AND THE HEADER OF `StrictRequestReader` USED TO TELL THE NEXT READER THAT EVERY ROUTE GROUP ALREADY
// BINDS THROUGH IT — an invitation to perform precisely that conversion, on the file a converter opens
// first. The header is corrected (`351274c`). **These tests are what makes the conversion LOUD**: after
// this file, converting `ReadJsonAsync` reddens the gate the way converting the support-login copy already
// did. Before it, the tenant login route would have broken in silence.
//
// **Do not delete these as duplicating the application-layer tests. Those construct the command directly
// and never serialize a byte; the defect these exist to catch lives entirely in the JSON binding.**
[Collection(PlatformSupportAuthenticationEndToEndGroup.Name)]
public sealed class PlatformAuthenticationEndToEndTests(PlatformSupportAuthenticationEndToEndHost host)
{
  private const string Origin = PlatformSupportAuthenticationEndToEndHost.Origin;
  private const string Password = PlatformSupportAuthenticationEndToEndHost.Password;
  private const string Prefix = "/api/platform/auth";

  // ================================================================================================
  // ⚠⚠⚠ THIS CLASS SHARES A SINGLETON RATE LIMITER WITH THE SUPPORT-LOGIN TESTS. READ BEFORE ADDING ONE.
  // ================================================================================================
  //
  // The E2E host is one process with one `AuthenticationEndpointRateLimiter`, and both surfaces log in over
  // the same loopback address, so **the `login-ip` partition is genuinely shared: 30 requests per rolling
  // minute across BOTH files.** Counted at the time of writing: 10 support logins (`:54`, `:88`, `:118`,
  // `:185`, and a SIX-case loop at `:165`) plus 3 here = 13. Headroom 17.
  //
  // ⚠ ORDER CANNOT MATTER BELOW THE LIMIT, AS A PROPERTY RATHER THAN AS A MEASUREMENT: a sliding-window
  // COUNT is invariant under permutation, so a fixed set of N calls inside one window gives the same
  // verdict in any order. **Order only becomes material at N >= 30** — and then it is intermittent and
  // order-dependent, which is the worst kind. Adding roughly seventeen more login tests to this collection
  // is what makes that reachable.
  //
  // ⚠⚠ AND THE RISK DIRECTION IS THE OPPOSITE OF THE ONE YOU WILL ASSUME. **The limiter reads WALL CLOCK
  // (`DateTimeOffset.UtcNow`), not this fixture's frozen `Now`.** So a FASTER machine is the hazard — it
  // compresses every login into one window — and a slower one slides them apart and passes.
  //
  // **A green run on a slow box is therefore the PERMISSIVE evidence, not the conservative evidence.** That
  // is exactly backwards from how a green run is normally read, which is why it is written here rather than
  // left to be re-derived. The identity partition (5 per 15 min) stays unreachable only because every test
  // below seeds a fresh email; reuse one and that limit binds instead.
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private static readonly DateTimeOffset Now = new(2026, 8, 12, 11, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Tenant_login_binds_a_camel_case_body_and_returns_the_single_eligible_tenant_session()
  {
    var (email, _, _) = await SeedTenantMemberAsync(tenantCount: 1);

    var response = await LoginAsync(email);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await ReadAsync<AuthenticatedResponse>(response);

    // ⚠ ASSERTING THE BOUND VALUES, NOT ONLY THE STATUS. A 200 alone would survive a reader that bound
    // nothing and a handler that happened to succeed anyway; the tenant and session identifiers can only
    // be non-empty if the credentials in the BODY were actually read.
    Assert.Equal("Authenticated", body.Outcome);
    Assert.Equal("Bearer", body.TokenType);
    Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    Assert.NotEqual(Guid.Empty, body.TenantId);
    Assert.True(body.TenantUserId > 0);
    Assert.True(body.AuthenticationSessionId > 0);
  }

  [Fact]
  public async Task Tenant_selection_binds_a_camel_case_body_and_establishes_the_chosen_tenant_session()
  {
    var (email, tenantIds, tenantUserIds) = await SeedTenantMemberAsync(tenantCount: 2);

    // Two eligible memberships, so login cannot auto-select and must hand back a reveal-once proof.
    var selection = await ReadAsync<TenantSelectionRequiredResponse>(await LoginAsync(email));
    Assert.Equal("TenantSelectionRequired", selection.Outcome);
    Assert.Equal(2, selection.Memberships.Count);

    var chosen = selection.Memberships[0];
    Assert.Contains(chosen.TenantId, tenantIds);
    Assert.Contains(chosen.TenantUserId, tenantUserIds);

    var response = await PostAsync("/select-tenant", JsonSerializer.Serialize(new
    {
      selectionProof = selection.SelectionProof,
      tenantId = chosen.TenantId,
      tenantUserId = chosen.TenantUserId
    }));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await ReadAsync<AuthenticatedResponse>(response);
    Assert.Equal("Authenticated", body.Outcome);

    // ⚠ THE DISCRIMINATING ASSERTION: the session is for the tenant the BODY named. A reader that bound
    // `tenantId` to `Guid.Empty` could not produce this, and a handler ignoring the request could not
    // either — this is the leg that fails if the JSON never reached the command.
    Assert.Equal(chosen.TenantId, body.TenantId);
    Assert.Equal(chosen.TenantUserId, body.TenantUserId);
    Assert.True(body.AuthenticationSessionId > 0);
  }

  [Fact]
  public async Task Tenant_selection_rejects_unknown_input_fields_without_echoing_the_body()
  {
    var (email, _, _) = await SeedTenantMemberAsync(tenantCount: 2);
    var selection = await ReadAsync<TenantSelectionRequiredResponse>(await LoginAsync(email));
    var chosen = selection.Memberships[0];

    // The same body as the happy path plus one field the contract does not declare. `select-tenant` has
    // never had this test; its sibling on `/login` is `HostEndpointTests:254`.
    var response = await PostAsync("/select-tenant", JsonSerializer.Serialize(new
    {
      selectionProof = selection.SelectionProof,
      tenantId = chosen.TenantId,
      tenantUserId = chosen.TenantUserId,
      clientId = "caller-value"
    }));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("request.invalid", body, StringComparison.Ordinal);
    Assert.DoesNotContain("caller-value", body, StringComparison.Ordinal);
  }

  // ==================================================================================================
  // `AC-AUTH-0049`'s FIRST CLAUSE — *"Every authentication response has the four approved security
  // headers…"* — WHICH NOTHING ASSERTED FOR THESE ROUTES.
  // ==================================================================================================
  //
  // ⚠⚠⚠ MEASURED BEFORE WRITING: `ApplyResponseSecurity`'s four assignments replaced by `_ = context;`.
  // **All seven suites green.** Every security header on the tenant authentication surface could be
  // deleted without reddening anything.
  //
  // Other modules DO assert these headers — `CompaniesEndpointTests`, `EmployeeEndpointTests` and others
  // check the same three names. ⚠ **That is what made the gap invisible: the property is visibly tested
  // ACROSS THE API, on every surface except this one**, and a reader checking whether "we test security
  // headers" finds nine hits and stops.
  //
  // ---- ⚠⚠ AND THE PLANT REVEALED TWO MECHANISMS, WHICH IS WHY THE EXPECTED VALUE IS A PARAMETER.
  //
  // Under the plant, `login`, `select-tenant` and `refresh` returned **no Cache-Control at all** — so on
  // this surface the headers come SOLELY from `ApplyResponseSecurity`; no global middleware covers it.
  // `logout` was **unchanged**, still `no-store, no-cache`.
  //
  // The reason is that `logout` alone carries `RequireAuthorization()`. An unauthenticated request is
  // refused by the pipeline and **the handler never runs**, so its headers come from the API-wide response
  // convention instead — the same `no-store, no-cache` the module suites assert.
  //
  // ***SO THE SAME API RETURNS TWO DIFFERENT CACHE-CONTROL VALUES FOR THE SAME CLAUSE, DECIDED BY WHICH
  // LAYER ANSWERED.*** Both are safe (`no-store` is the stricter), and neither is wrong under a criterion
  // that names the header rather than its value — **but a test asserting one uniform string would have
  // been false, and a test asserting only the three uniform headers would have hidden the split.** Each
  // row therefore pins its own value, and the `logout` row is the only witness for the second mechanism.
  [Theory]
  [InlineData("/login", "no-store")]
  [InlineData("/select-tenant", "no-store")]
  [InlineData("/refresh", "no-store")]
  [InlineData("/logout", "no-store, no-cache")]
  [Trait("Criterion", "AC-AUTH-0049")]
  public async Task Every_authentication_route_answers_with_the_four_approved_security_headers(
    string path,
    string expectedCacheControl)
  {
    // A `text/plain` body is refused before the rate limiter is consulted on all four routes, so this
    // theory costs nothing against the 30-per-minute `login-ip` budget documented above. **It also makes
    // the assertion the stronger one: the headers are present on the REFUSAL path, which is where a
    // cacheable error response would actually leak.**
    using var request = new HttpRequestMessage(HttpMethod.Post, $"{Prefix}{path}")
    {
      Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
    };
    request.Headers.Add("Origin", Origin);

    var response = await host.Client.SendAsync(request);

    Assert.Equal(expectedCacheControl, response.Headers.CacheControl?.ToString());
    Assert.Equal("no-cache", response.Headers.Pragma.ToString());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0004")]
  // ==================================================================================================
  // `AC-AUTH-0004` — *"A tenant without ACTIVE MEMBERSHIP cannot be selected."*
  // ==================================================================================================
  //
  // ⚠⚠⚠ THE CRITERION IS ENFORCED TWICE, AND THIS TEST WITNESSES THE **CONJUNCTION**, NOT EITHER SITE.
  //
  //   A  `ListEligibleMembershipsAsync`           LINQ `.Where(… user.Status == Active)` — what is OFFERED
  //   B  `GetMembershipEligibilityForUpdateAsync` raw SQL `AND [Status] = N'Active'` under `UPDLOCK` —
  //                                               what may be SELECTED INTO, revalidated at the moment of use
  //
  // THE MEASURED MATRIX, because I twice guessed it wrong before running it:
  //
  //   plant        this test                       the listing test
  //   -----        ---------                       ----------------
  //   none         pass                            pass
  //   A            pass  (B refuses instead)       **RED** — 3 memberships offered, not 2
  //   B            pass  (A excludes it first)     pass
  //   A + B        **RED**                         RED
  //
  // ***SO NEITHER SITE IS INDIVIDUALLY WITNESSED BY THIS TEST — EACH IS SUFFICIENT, SO REMOVING EITHER
  // CHANGES NOTHING OBSERVABLE HERE.*** That is the redundancy topology in its pure form, and the lesson is
  // about the METHOD: **a green after a plant means "something else also enforces this", never "nothing
  // does", and the two are indistinguishable without planting the rest of the set.** I read the first green
  // as "unwitnessed" and was wrong; then read the 401 as coming from B and was wrong again.
  //
  // Site A is witnessed on its own by `Login_offering_multiple_memberships_omits_a_deactivated_one`.
  // Site B is witnessed on its own by `Tenant_selection_is_refused_when_the_membership_is_deactivated_after_login`,
  // which is the only shape that can: it needs the membership to pass the listing and fail at use.
  //
  // ⚠⚠ AND THE TWO TESTS THAT LOOK LIKE THIS ONE CANNOT WITNESS IT — for two DIFFERENT reasons, which is
  // why neither gap was visible:
  //
  //   `Begin_tenant_access_returns_no_membership_without_creating_authentication_state` seeds **ZERO**
  //   memberships. ***A TEST OF THE NULL CASE CANNOT WITNESS A CLAIM ABOUT HOW THE NON-NULL CASES
  //   DIFFER*** — with no membership at all, active and inactive are indistinguishable.
  //
  //   `Suspended_tenant_is_refused_at_tenant_selection` sets the fixture's `TenantEligible` false. **That
  //   is the TENANT's status, not the MEMBERSHIP's** — a different column, a different join, a different
  //   criterion (`AC-AUTH-0018`). The two read as the same English sentence and are not the same claim.
  //
  // So the discriminating fixture is a membership that EXISTS and is DEACTIVATED, which nothing built.
  // `TenantUser.Deactivate` supplies it, and the seeding path is otherwise identical to the passing
  // login tests — **the only difference between this test and a successful login is the membership's
  // status**, which is what makes the 401 attributable to it.
  //
  // ⚠ Costs one login against the 30-per-minute `login-ip` budget documented above; headroom noted there
  // was 17 at the time of writing, and this file's count rises by one.
  public async Task Login_with_a_deactivated_membership_is_refused_and_creates_no_session()
  {
    var (email, _, tenantUserIds) = await SeedTenantMemberAsync(tenantCount: 1, deactivateMembership: true);

    var response = await LoginAsync(email);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("authentication.failed", body, StringComparison.Ordinal);

    // The criterion's second half: refused AND nothing created. A handler that answered 401 after issuing
    // a session would pass the assertions above.
    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var tenantUserId = tenantUserIds[0];
    Assert.Empty(await context.AuthenticationSessions.AsNoTracking()
      .Where(session => session.TenantUserId == tenantUserId)
      .ToArrayAsync());
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0004")]
  // `AC-AUTH-0004`'s LISTING half, and the reason it needs its own test: with TWO memberships the user is
  // OFFERED a choice instead of being auto-selected, **so `ListEligibleMembershipsAsync` is the only gate
  // the response passes through** and the for-update revalidation never runs. One membership is
  // deactivated; the criterion is that it is not on the menu.
  //
  // ⚠ The active one is asserted present in the same breath. Without it, a listing that returned NOTHING
  // would satisfy "the deactivated one is absent" — and the login would then have failed outright rather
  // than offering a selection, which is a different response this test would not distinguish.
  public async Task Login_offering_multiple_memberships_omits_a_deactivated_one()
  {
    // THREE memberships, one deactivated. ⚠ TWO would not do: deactivating one of two leaves a single
    // active membership, which is AUTO-SELECTED — the response is then an authenticated session and the
    // listing is never rendered at all. **The fixture size is what decides which code path is under test**,
    // and the first version of this test asserted against a response shape the product never produced.
    var (email, tenantIds, _) = await SeedTenantMemberAsync(tenantCount: 3, deactivateMembership: true, deactivateOnlyIndex: 2);

    var response = await LoginAsync(email);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var selection = await ReadAsync<TenantSelectionRequiredResponse>(response);

    Assert.Equal(2, selection.Memberships.Count);
    Assert.DoesNotContain(selection.Memberships, membership => membership.TenantId == tenantIds[2]);
    Assert.Contains(selection.Memberships, membership => membership.TenantId == tenantIds[0]);
    Assert.Contains(selection.Memberships, membership => membership.TenantId == tenantIds[1]);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0004")]
  // `AC-AUTH-0004` AT THE MOMENT OF USE — and the only shape that isolates the for-update revalidation.
  //
  // Login lists two eligible memberships and hands back a selection proof. **The membership is then
  // deactivated, after it has already been offered.** The subsequent `select-tenant` must refuse it.
  //
  // ⚠⚠ THIS IS THE ONLY TEST IN WHICH THE LISTING FILTER CANNOT HELP: the row passed that filter, legally,
  // before it changed. **Every other fixture deactivates BEFORE the listing, where site A refuses first and
  // site B is never reached** — which is exactly why site B had no independent witness.
  //
  // ⚠ And it is the scenario the `UPDLOCK`/`HOLDLOCK` revalidation exists for. A design that trusted the
  // proof would issue a session into a membership that was revoked while the user was looking at the menu;
  // the window is however long the person takes to click.
  //
  // PLANT: the raw-SQL `AND [Status] = N'Active'` removed — this test reddens, `Expected Unauthorized /
  // Actual ServiceUnavailable`. ⚠⚠ **THE PLANT DOES NOT PRODUCE A PERMISSIVE 200; IT PRODUCES A 503**,
  // because with the row returned the flow proceeds and fails further down. So removing site B is LOUD
  // rather than silent — *which is precisely why the assertion above must be the exact refusal.* A
  // `NotEqual(OK)` would have called that 503 a pass and reported this test as witnessing a criterion it
  // was no longer testing.
  public async Task Tenant_selection_is_refused_when_the_membership_is_deactivated_after_login()
  {
    var (email, tenantIds, tenantUserIds) = await SeedTenantMemberAsync(tenantCount: 2);
    var selection = await ReadAsync<TenantSelectionRequiredResponse>(await LoginAsync(email));
    Assert.Equal(2, selection.Memberships.Count);
    var chosen = selection.Memberships[0];

    await DeactivateMembershipAsync(chosen.TenantUserId, chosen.TenantId);

    var response = await PostAsync("/select-tenant", JsonSerializer.Serialize(new
    {
      selectionProof = selection.SelectionProof,
      tenantId = chosen.TenantId,
      tenantUserId = chosen.TenantUserId
    }));

    // ⚠⚠ THE EXACT REFUSAL, NOT MERELY "NOT OK". `Assert.NotEqual(OK)` was the first version and it is
    // satisfied by a CRASH: with the for-update status filter removed this route answers **503**, and a
    // not-OK assertion reports that as a pass. ***A NEGATIVE ASSERTION ABOUT A STATUS CODE CANNOT
    // DISTINGUISH A CORRECT REFUSAL FROM A FAILURE TO ANSWER*** — measured, not anticipated.
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Contains("authentication.selection_failed", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

    // Refused AND nothing created. The seeded ids are used rather than the response body, which on a
    // refusal carries neither.
    Assert.Contains(chosen.TenantId, tenantIds);
    Assert.Contains(chosen.TenantUserId, tenantUserIds);
    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var tenantUserId = chosen.TenantUserId;
    Assert.Empty(await context.AuthenticationSessions.AsNoTracking()
      .Where(session => session.TenantUserId == tenantUserId)
      .ToArrayAsync());
  }

  private async Task DeactivateMembershipAsync(long tenantUserId, Guid tenantId)
  {
    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
    accessor.HttpContext = TenantContext(tenantId);
    var membership = await context.TenantUsers.SingleAsync(user => user.Id == tenantUserId);
    Assert.True(membership.Deactivate(Guid.NewGuid(), Now.AddMinutes(3)).IsSuccess);
    await context.SaveChangesAsync();
    accessor.HttpContext = null;
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0042")]
  // ==================================================================================================
  // `AC-AUTH-0042`'s *"REFRESH AND LOGOUT **REQUIRE** THE … CSRF COOKIE/HEADER PAIR"* — THE CLAUSE THAT HAD
  // NO WITNESS, AND THE ONLY SHAPE THAT CAN CARRY IT.
  // ==================================================================================================
  //
  // ⚠⚠⚠ EVERY EXISTING CSRF TEST DRIVES `AuthenticationCsrfService` DIRECTLY. They are good tests —
  // tampered header, absent header, empty cookie, wrong selector, wrong ClientId, expired payload,
  // rotation — and **they prove the SERVICE refuses. Not one proves either ENDPOINT asks it.** The
  // criterion's verb is REQUIRE, which is a claim about the caller.
  //
  // MEASURED: `!csrf.TryValidate(…) && false` in the refresh route — the call still runs, its answer
  // discarded — left all seven suites green.
  //
  // ⚠⚠ AND THE ENFORCEMENT SET WAS ENUMERATED BEFORE THAT GREEN WAS BELIEVED, because a green after a
  // plant means *something else also enforces this* unless the set has one member. `TryValidate` has
  // **exactly four call sites in `src/`** — tenant refresh, tenant logout, support refresh, support logout
  // — `AuthenticationCsrfService` is the only CSRF implementation and is registered once, and there is no
  // `IAntiforgery`, `UseAntiforgery` or `ValidateAntiForgeryToken` anywhere in the tree. **So for THIS
  // route the set has size one, the plant removed the only member, and the green is a statement about the
  // tests rather than about a sibling holding the property up.**
  //
  // ⚠ WHAT THE PLANT DOES NOT REMOVE, stated so the claim is not read wider than it is: the refresh-cookie
  // PRESENCE check survives it. A request with no refresh cookie is still refused.
  //
  // ⚠⚠⚠ AND A CORRECTION TO WHAT THE PLANT SHOWS, MEASURED AGAINST THIS TEST: the planted build answers
  // **500, not 200.** `TryValidate`'s `out` parameter is a record CLASS, so on the discarded-failure path
  // `csrfPayload` is null and the next line dereferences it for the rate-limit partition key.
  //
  // ***SO THE PLANT ESTABLISHES "NOTHING WATCHED THIS CHECK", NOT "A CSRF-LESS REFRESH WOULD SUCCEED".***
  // Those are different claims and the second is the alarming one. **This test asserts the refusal
  // DIRECTLY — `Forbidden` plus the exact code — so it does not depend on which way a broken build
  // happens to fail.** A weaker assertion here (`NotEqual(OK)`) would have been satisfied by that 500 and
  // would have gone on reporting itself as a witness.
  //
  // THE CONTROL IS THE SECOND HALF OF THIS TEST and is not decoration: the identical request WITH the
  // header must succeed. Without it, a route that refused everything — a broken cookie jar, a wrong path,
  // a rate limit — would satisfy the refusal and read as a witness.
  public async Task Refresh_requires_the_csrf_header_even_with_a_valid_refresh_cookie()
  {
    var (email, _, _) = await SeedTenantMemberAsync(tenantCount: 1);
    var login = await LoginAsync(email);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var cookies = CookieJar(login);
    Assert.True(cookies.ContainsKey("__Secure-ssas-refresh"), "login did not set the refresh cookie");
    Assert.True(cookies.ContainsKey("__Secure-ssas-xsrf"), "login did not set the CSRF cookie");

    var withoutHeader = await SendWithCookiesAsync("/refresh", cookies, includeCsrfHeader: false);

    Assert.Equal(HttpStatusCode.Forbidden, withoutHeader.StatusCode);
    Assert.Contains("authentication.request_rejected",
      await withoutHeader.Content.ReadAsStringAsync(), StringComparison.Ordinal);

    // ---- THE CONTROL: same cookies, same route, header present.
    var withHeader = await SendWithCookiesAsync("/refresh", cookies, includeCsrfHeader: true);
    Assert.Equal(HttpStatusCode.OK, withHeader.StatusCode);
  }

  private Task<HttpResponseMessage> SendWithCookiesAsync(
    string path,
    Dictionary<string, string> cookies,
    bool includeCsrfHeader)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, $"{Prefix}{path}");
    request.Headers.Add("Origin", Origin);
    request.Headers.Add("Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
    if (includeCsrfHeader && cookies.TryGetValue("__Secure-ssas-xsrf", out var csrf))
    {
      request.Headers.Add("X-XSRF-TOKEN", csrf);
    }

    return host.Client.SendAsync(request);
  }

  private static Dictionary<string, string> CookieJar(HttpResponseMessage response)
  {
    var jar = new Dictionary<string, string>(StringComparer.Ordinal);
    if (!response.Headers.TryGetValues("Set-Cookie", out var headers)) return jar;
    foreach (var header in headers)
    {
      var pair = header.Split(';', 2)[0];
      var separator = pair.IndexOf('=', StringComparison.Ordinal);
      if (separator > 0) jar[pair[..separator]] = pair[(separator + 1)..];
    }

    return jar;
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0006")]
  // ==================================================================================================
  // `AC-AUTH-0006` — *"Roles and permissions belong ONLY to the SELECTED tenant."*
  // ==================================================================================================
  //
  // ⚠⚠⚠ MEASURED FIRST: `AccessTokenClaimsProvider` runs its role and permission queries under
  // `IgnoreQueryFilters()`, so the global tenant filter is off and the hand-written predicates are the only
  // tenant scoping there is. **Removing all three `TenantId == tenantId` predicates left seven suites
  // green** — before this test existed. It reddens them now.
  //
  // ⚠⚠ AND THE PLANT SEQUENCE IS WORTH THE SPACE, BECAUSE IT TOOK FOUR TRIES TO MAKE THIS TEST FAIL AND
  // EACH GREEN MEANT SOMETHING DIFFERENT. The role path has THREE MUTUALLY SUFFICIENT scoping predicates:
  //
  //   `assignment.TenantId == tenantId`          on the role assignment
  //   `assignment.TenantUserId == tenantUserId`  — **already tenant-scoping, because one identity has a
  //                                              SEPARATE `TenantUser` row per tenant**
  //   `role.TenantId == tenantId`                on the role itself
  //
  //   removed: the three TenantId predicates   -> still green; `TenantUserId` alone kept it true
  //   removed: TenantUserId + role.TenantId     -> still green; `assignment.TenantId` alone kept it true
  //   removed: all three on the role path       -> **RED: `["ROLE-A", "ROLE-B"]`**
  //
  // ***SO NO INDIVIDUAL PREDICATE IS WITNESSED, AND THAT IS INHERENT TO REDUNDANCY RATHER THAN A DEFECT IN
  // THE TEST.*** This test witnesses the CRITERION — the observable property that a token carries only the
  // selected tenant's grants — which is what the criterion states. **A test that isolated one predicate
  // would have to break the other two first, which is not a state the product can be in.**
  //
  // ⚠ What remains genuinely unwitnessed is the defence-in-depth case those predicates exist for: a
  // CORRUPT row — an assignment in tenant A pointing at a role in tenant B — which no well-formed fixture
  // can produce. The analogous test exists for the platform-support plane
  // (`Corrupt_platform_support_assignment_is_excluded_from_tenant_access_token_claims`, `AC-TEN-0030`), and
  // it uses raw SQL to force the row. Named rather than left as a silent gap.
  //
  // ⚠⚠ AND THE TWO INTEGRATION CALL SITES I READ CANNOT DISCRIMINATE EITHER. `EmployeeBoundarySqlServer
  // Tests.ClaimedPermissionsAsync` and `PlatformAuthenticationPersistenceTests` both drive the real
  // provider — **each against a SINGLE tenant.** *A one-tenant fixture cannot witness a claim about which
  // tenant's rows are excluded*, the same shape as a one-session `only` and a zero-membership `cannot`.
  // (I have not read every Integration call site; that is the bound on this sentence.)
  //
  // ⚠ `PlatformReadScopeArchitectureTests` names this file as a hand-written-predicate reader — which
  // asserts a predicate EXISTS, not that it is on the right column of the right table, and not that all
  // three survive. **Structural cover for a behavioural claim.**
  //
  // THE FIXTURE IS THE CRITERION: one identity, two active tenants, a role and permission granted in
  // **each**. Claims for tenant A must carry A's role and A's permission and neither of B's. ⚠ Granting in
  // BOTH is what makes it a scoping test rather than an emptiness test — **with a role only in B, "no
  // roles for A" is also satisfied by a provider that returns nothing at all.**
  public async Task Access_token_claims_carry_only_the_selected_tenants_roles_and_permissions()
  {
    var (_, tenantIds, tenantUserIds, identityId) = await SeedRoleGrantsInTwoTenantsAsync();

    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var session = AuthenticationSession.Create(
      identityId, tenantUserIds[0], tenantIds[0], "ssas-erp-web", Guid.NewGuid(), 1,
      Now, Now.AddDays(30), Now.AddDays(90));
    context.AuthenticationSessions.Add(session);
    await context.SaveChangesAsync();

    var claims = await new AccessTokenClaimsProvider(context, new PlatformPermissionCatalog()).GetClaimsAsync(
      session.Id, identityId, tenantUserIds[0], tenantIds[0],
      AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value, 1);

    Assert.True(claims.IsSuccess, claims.IsFailure ? claims.Error.Code : null);
    // The POSITIVE half: tenant A's grant is present, so an empty answer cannot pass.
    Assert.Equal(["ROLE-A"], claims.Value.Roles);
    Assert.Contains(PlatformPermissionNames.ViewCompanies, claims.Value.Permissions);
    // The criterion: tenant B's grant is absent.
    Assert.DoesNotContain("ROLE-B", claims.Value.Roles);
    Assert.DoesNotContain(PlatformPermissionNames.ViewRoles, claims.Value.Permissions);
  }

  private async Task<(string Email, Guid[] TenantIds, long[] TenantUserIds, long IdentityId)>
    SeedRoleGrantsInTwoTenantsAsync()
  {
    var (email, tenantIds, tenantUserIds) = await SeedTenantMemberAsync(tenantCount: 2);
    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
    var identityId = await context.TenantUsers.IgnoreQueryFilters()
      .Where(user => user.Id == tenantUserIds[0]).Select(user => user.IdentityId).SingleAsync();

    var catalog = new PlatformPermissionCatalog();
    var grants = new[]
    {
      (Index: 0, RoleName: "ROLE-A", Permission: PlatformPermissionNames.ViewCompanies),
      (Index: 1, RoleName: "ROLE-B", Permission: PlatformPermissionNames.ViewRoles)
    };

    foreach (var (index, roleName, permission) in grants)
    {
      accessor.HttpContext = TenantContext(tenantIds[index]);
      var role = Role.CreateCustom(
        tenantIds[index], RoleName.Create(roleName).Value, null, Guid.NewGuid(), Now);
      Assert.True(catalog.TryGet(permission, out var definition));
      Assert.True(role.AssignPermission(definition, "e2e-seed", Guid.NewGuid(), Now).IsSuccess);
      context.Roles.Add(role);
      await context.SaveChangesAsync();

      var member = await context.TenantUsers.IgnoreQueryFilters()
        .SingleAsync(user => user.Id == tenantUserIds[index]);
      Assert.True(member.AssignRole(role, "e2e-seed", Guid.NewGuid(), Now).IsSuccess);
      await context.SaveChangesAsync();
      accessor.HttpContext = null;
    }

    return (email, tenantIds, tenantUserIds, identityId);
  }

  // ---- SEEDING.
  //
  // ⚠ `TenantUser` is tenant-owned, and `PersistenceDbContext.AssignTenant` REFUSES to save one without a
  // trusted tenant context — `CurrentTenant` reads the tenant claim off `IHttpContextAccessor`, which is
  // null outside a request. So the seeding scope installs an authenticated principal carrying the tenant
  // claim. That is the production accessor doing its real job, not a bypass: the write still has to match.
  private async Task<(string Email, Guid[] TenantIds, long[] TenantUserIds)> SeedTenantMemberAsync(
    int tenantCount,
    bool deactivateMembership = false,
    int? deactivateOnlyIndex = null)
  {
    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
    var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
    var email = $"member-{Guid.NewGuid():N}@example.test";

    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    await context.SaveChangesAsync();

    var account = AuthenticationAccount.CreatePending(identity.Id, LoginEmail.Create(email).Value);
    Assert.True(account.CompleteInitialSetup(hashing.HashPassword(Password), Guid.NewGuid(), Now).IsSuccess);
    context.AuthenticationAccounts.Add(account);
    await context.SaveChangesAsync();

    var tenantIds = new List<Guid>();
    var tenantUserIds = new List<long>();
    for (var index = 0; index < tenantCount; index++)
    {
      var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
      var tenant = Tenant.Create(
        TenantCode.Create(code).Value,
        TenantName.Create($"Tenant {code}").Value,
        "e2e-seed",
        Guid.NewGuid(),
        Now).Value;
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      // ⚠ ACTIVATION IS LOAD-BEARING AND WAS PROVEN SO. An inactive tenant is not an eligible membership
      // (`IdentityTenantMembershipReadService` joins on `TenantStatus.Active`), so removing these two
      // lines reddens ALL THREE tests — measured, because all three passed on their first run in 370ms and
      // a happy-path test on a route nothing had ever exercised positively is where a vacuous test is born.
      Assert.True(tenant.Activate("e2e-seed", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
      await context.SaveChangesAsync();

      accessor.HttpContext = TenantContext(tenant.Id);
      var membership = TenantUser.CreateActive(
        identity.Id,
        tenant.Id,
        EmailAddress.Create(email).Value,
        UserDisplayName.Create($"Member {code}").Value,
        Guid.NewGuid(),
        Now);
      context.TenantUsers.Add(membership);
      await context.SaveChangesAsync();
      // ⚠ DEACTIVATED AFTER THE INSERT, NOT INSTEAD OF IT. The row must EXIST and be non-Active for
      // `AC-AUTH-0004` to have a subject; a membership that was never created tests the null case.
      if (deactivateMembership && (deactivateOnlyIndex is null || deactivateOnlyIndex == index))
      {
        Assert.True(membership.Deactivate(Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        await context.SaveChangesAsync();
      }

      accessor.HttpContext = null;

      tenantIds.Add(tenant.Id);
      tenantUserIds.Add(membership.Id);
    }

    return (email, [.. tenantIds], [.. tenantUserIds]);
  }

  private static DefaultHttpContext TenantContext(Guid tenantId) => new()
  {
    User = new ClaimsPrincipal(new ClaimsIdentity(
      [new Claim(JwtClaimTypes.TenantId, tenantId.ToString())], "e2e-seed"))
  };

  private Task<HttpResponseMessage> LoginAsync(string loginEmail) =>
    PostAsync("/login", JsonSerializer.Serialize(new { loginEmail, password = Password }));

  private Task<HttpResponseMessage> PostAsync(string path, string payload)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, $"{Prefix}{path}")
    {
      Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Origin", Origin);
    return host.Client.SendAsync(request);
  }

  private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
  {
    var body = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(body, JsonOptions)
      ?? throw new InvalidOperationException($"the response body did not deserialize to {typeof(T).Name}: {body}");
  }
}
