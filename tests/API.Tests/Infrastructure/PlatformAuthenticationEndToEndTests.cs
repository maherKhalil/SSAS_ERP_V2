using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Domain.Authentication;
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

  // ---- SEEDING.
  //
  // ⚠ `TenantUser` is tenant-owned, and `PersistenceDbContext.AssignTenant` REFUSES to save one without a
  // trusted tenant context — `CurrentTenant` reads the tenant claim off `IHttpContextAccessor`, which is
  // null outside a request. So the seeding scope installs an authenticated principal carrying the tenant
  // claim. That is the production accessor doing its real job, not a bypass: the write still has to match.
  private async Task<(string Email, Guid[] TenantIds, long[] TenantUserIds)> SeedTenantMemberAsync(int tenantCount)
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
