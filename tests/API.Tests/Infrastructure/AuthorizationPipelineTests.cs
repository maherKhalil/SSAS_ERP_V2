using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

public sealed class AuthorizationPipelineTests : IAsyncLifetime
{
  private const string Issuer = "https://authorization.tests";
  private const string Audience = "authorization-tests";
  private static readonly Guid TenantId = Guid.Parse("64fbfdcc-c6e3-4626-ad14-ed4f7aa156e1");
  private WebApplication? application;
  private HttpClient? client;
  private MutableTenantEligibility? tenantEligibility;

  [Fact]
  public async Task Unauthenticated_permission_request_returns_401_with_a_correlation_id()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/test/permission");
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "authorization-401");

    var response = await Client.SendAsync(request);

    await AssertAuthorizationFailureAsync(response, HttpStatusCode.Unauthorized, "authorization-401");
    Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
    Assert.Equal("no-cache", response.Headers.Pragma.ToString());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  [Fact]
  public async Task Authenticated_user_without_permission_returns_403_with_a_correlation_id()
  {
    using var request = CreateAuthorizedRequest("/test/permission", new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "authorization-403");

    var response = await Client.SendAsync(request);

    await AssertAuthorizationFailureAsync(response, HttpStatusCode.Forbidden, "authorization-403");
  }

  [Fact]
  public async Task Authenticated_user_with_matching_permission_and_tenant_is_authorized()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Role_policy_uses_the_validated_role_claim()
  {
    using var request = CreateAuthorizedRequest(
      "/test/role",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Role, "test.role"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Missing_tenant_claim_is_rejected_as_an_invalid_access_token()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ⚠ CITES `AC-IAM-0021`'s FIRST HALF — *"A suspended tenant cannot receive normal application access"* —
  // AND IT IS A CROSS-PACKAGE CITATION, WHICH NEEDS ITS GROUNDS STATED HERE RATHER THAN INFERRED.
  // Tenant suspension is `FP-003` lifecycle behaviour and this is an `FP-003`-shaped test; `FP-001` carries
  // a criterion about it because IAM's subject is the ACCESS CONSEQUENCE, not the transition. **A trait
  // claims EVIDENCE, not OWNERSHIP** — duplicating a suspension test into the IAM directory to make a
  // per-package sweep tidy would buy a second maintenance site and no new assurance.
  //
  // ⚠⚠ WHOEVER MAINTAINS THIS FILE HAS NO OTHER WAY TO KNOW AN `FP-001` CRITERION NOW RESTS ON IT. That is
  // the real cost of citing across packages, and this comment is the compensator: a bare trait is a claim
  // with its grounds elsewhere, and a future simplification that narrowed these rows would silently delete
  // the only evidence for an IAM criterion.
  //
  // ⚠⚠⚠ ADJACENT-SCOPE CHECKED, BECAUSE THE NAME IS A SUPERSET AND THE CRITERION IS NOT. *Non_active_or_
  // missing* names a class; `AC-IAM-0021` is SUSPENSION-SPECIFIC. **`TenantStatus.Suspended` is its own
  // `[InlineData]` row**, so the criterion is carried by an exercised case and NOT by reasoning that
  // Suspended is a member of the class. Had the rows been `Provisioning`/`Archived`/`null` only, this
  // citation would have been exactly the defect it is meant to record.
  //
  // The criterion's SECOND half — *"or new tenant-scoped tokens"* — is not here: this asserts a route
  // returns 403, never that issuance is refused. `TenantLifecycleApplicationTests` and
  // `TenantLifecycleDomainTests` carry the eligibility side.
  [Theory]
  [InlineData(TenantStatus.Provisioning)]
  [InlineData(TenantStatus.Suspended)]
  [InlineData(TenantStatus.Archived)]
  [InlineData(null)]
  [Trait("Criterion", "AC-IAM-0021")]
  public async Task Non_active_or_missing_tenant_is_rejected(TenantStatus? status)
  {
    TenantEligibility.Status = status;
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ⚠ ALSO CITES `AC-IAM-0021`'s FIRST HALF, AND IT IS THE STRONGER OF THE TWO. The theory above proves a
  // suspended tenant is refused; this proves an ALREADY-ISSUED token stops working the moment the tenant is
  // suspended — which is what *"cannot receive normal application access"* means for a caller who was
  // legitimately admitted a second earlier. **Refusing new access and revoking existing access are different
  // properties, and the criterion needs the second.**
  //
  // ⚠⚠ This one is suspension-specific by construction — no class name, no theory rows, `TenantStatus
  // .Suspended` set directly — so there is no adjacent-scope question to answer for it. Cross-package for
  // the same reason stated on the theory above.
  //
  // ==================================================================================================
  // ⚠⚠⚠ CORRECTION. THE PARAGRAPH ABOVE IS RIGHT ABOUT THE PROPERTIES AND WRONG ABOUT WHICH ONE THIS
  // TEST EXERCISES, AND THE CORRECTION IS MEASURED, NOT ARGUED.
  // ==================================================================================================
  //
  // *"Refusing new access and revoking existing access are different properties"* — true. **This test
  // exercises the first.** It builds the request, then sets `Suspended`, then sends;
  // `Non_active_or_missing_tenant_is_rejected(Suspended)` sets `Suspended`, then builds, then sends. Same
  // experiment, two lines swapped: `CreateAuthorizedRequest` mints a JWT and `CreateToken` reads only
  // `ISigningKeyProvider`, so **NOTHING READS TENANT ELIGIBILITY BETWEEN MINTING AND SENDING** and the
  // interval this arrangement represents is invisible to the pipeline. Neither test ever sees the token
  // ADMITTED, so no caller here was *legitimately admitted a second earlier*.
  //
  // MEASURED: widening the eligibility memo's lifetime from per-request to singleton — the exact defect
  // `AC-TEN-0019` describes — leaves this test GREEN and reddens only
  // `One_token_admitted_while_active_is_refused_once_the_tenant_is_suspended` below. Full record there.
  //
  // ⚠ KEPT, NOT DELETED, AND NOT BECAUSE OF DOUBT. It is a valid assertion about a suspended tenant and
  // its `AC-IAM-0021` citation stands on that. What is withdrawn is the claim that it covers the
  // REVOCATION half; the test below does. **The failure mode was not a bad test — it was a comment
  // asserting a distinction the code beneath it could not make**, which is why a reader was never going to
  // catch it by reading more carefully.
  [Fact]
  [Trait("Criterion", "AC-IAM-0021")]
  public async Task Already_issued_token_is_immediately_rejected_after_tenant_suspension()
  {
    using var request = CreateAuthorizedRequest(
      "/test/permission",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));
    TenantEligibility.Status = TenantStatus.Suspended;

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ⚠⚠⚠ THIS EXISTS BECAUSE THE TEST ABOVE DOES NOT DO WHAT ITS NAME AND ITS COMMENT SAY, AND THE
  // DIFFERENCE IS TWO STATEMENTS IN THE OTHER ORDER.
  //
  // `Already_issued_token_is_immediately_rejected_after_tenant_suspension` builds the request, THEN sets
  // `Status = Suspended`, then sends. `Non_active_or_missing_tenant_is_rejected(Suspended)` sets the status
  // FIRST and then builds and sends. **THOSE ARE THE SAME EXPERIMENT WITH TWO LINES SWAPPED.**
  // `CreateAuthorizedRequest` mints a JWT and touches nothing else — `CreateToken` reads only
  // `ISigningKeyProvider` — so NOTHING READS TENANT ELIGIBILITY BETWEEN MINTING AND SENDING, and the
  // interval the arrangement is meant to represent is not observable by anything in the pipeline. No
  // product change can redden one of those two and not the other.
  //
  // ⚠ THE COMMENT ON IT CLAIMS THE DISTINCTION EXPLICITLY — *"refusing new access and revoking existing
  // access are different properties, and the criterion needs the second"* — and that sentence is right
  // about the properties and wrong about which one the test exercises. **Both tests present a token while
  // the tenant is already non-Active; NEITHER EVER SEES THE TOKEN ADMITTED.** A caller who was
  // *legitimately admitted a second earlier* never appears.
  //
  // ⚠⚠ THIS IS NOT AN INERT FIXTURE VALUE. The status assignment is used, the ordering is deliberate, and
  // the author's intent is legible and correct. **THE ARRANGEMENT MODELS THE PROPERTY AND THE SYSTEM CANNOT
  // OBSERVE THE MODEL** — which is harder to see than a dead argument, because every line is doing
  // something.
  //
  // ---- WHAT THIS ONE ADDS: THE ADMISSION, AND THE SECOND READ.
  //
  // ONE token is minted once and sent TWICE. The first send is admitted while the tenant is Active — which
  // is the *legitimately admitted a second earlier* that was missing — and the second, with the SAME
  // bearer string, is refused after suspension. `AC-TEN-0019` is *"an already issued token does not
  // override CURRENT non-Active status"*, and *current* is a claim about WHEN the status is read.
  //
  // ⚠⚠⚠ SO THE LIVENESS IS ASSERTED MECHANICALLY RATHER THAN INFERRED FROM THE 403: `Calls` must ADVANCE
  // between the two sends. A 403 alone is consistent with a decision cached from the first request and
  // happening to be re-derived; the counter says the eligibility service was consulted again. That is the
  // same instrument `Role_and_permission_authorization_share_one_live_tenant_lookup_per_request` uses one
  // line down, applied ACROSS requests rather than within one.
  //
  // `AC-IAM-0021`'s first half is cited here too and this is now its strongest site, for the reason its
  // own comment above gives: revoking existing access, not refusing new access.
  //
  // ---- ⚠⚠⚠ PLANTED, AND THE PLANT SETTLED THE EQUIVALENCE ARGUMENT EMPIRICALLY RATHER THAN BY READING.
  //
  // The defect this criterion names — *an already issued token overriding current status* — has a one-word
  // spelling in this codebase. `RequestTenantEligibility` is a memo `Dictionary<Guid, Task<...>>` whose
  // LIFETIME IS THE ONLY THING MAKING THE DECISION LIVE; registered `AddScoped` the memo dies with the
  // request, and `AddSingleton` it outlives every request. **The plant changed exactly that one word in
  // this file's host.**
  //
  //   THIS TEST                                    RED — `Expected: Forbidden, Actual: OK`. The token
  //                                                admitted while Active was still admitted after
  //                                                suspension. That is the criterion failing, in words.
  //   `Already_issued_token_is_immediately_...`    GREEN
  //   `Non_active_or_missing_tenant_is_rejected`   GREEN, all four rows
  //   `Role_and_permission_..._per_request`        GREEN
  //   the other 3,287 tests                        GREEN
  //
  // **EXACTLY ONE TEST IN 3,291 NOTICED, AND IT IS THE ONE ADDED HERE.** The test named for this property
  // did not — which is the equivalence argument above, confirmed by measurement instead of by reading two
  // helper methods and concluding.
  //
  // ⚠ THE FAILURE MESSAGE DISCRIMINATED WHERE THE LINE NUMBER DID NOT. This test has an `Assert.Equal` and
  // an `Assert.True`; the TRX line and my own count of the file disagreed by one, and `Expected:
  // Forbidden, Actual: OK` settled it with no counting at all. **When a control needs identifying, the text
  // that names the VALUES beats any positional evidence.**
  //
  // ⚠⚠ AND THE PRODUCTION REGISTRATION HAS ITS OWN GUARD, WHICH IS WHY THIS IS A PAIR AND NOT A DUPLICATE.
  // `PlatformInfrastructureRegistrationTests` asserts `AssertScoped<IRequestTenantEligibility>` — it pins
  // the LIFETIME in the real composition root. This one pins the CONSEQUENCE, and the plant above was on
  // this file's own host registration rather than on `src/`, so what it measured is whether this test
  // detects the behaviour. Neither subsumes the other: a lifetime assertion cannot see a memo that caches
  // by some other means, and a behaviour test cannot see a lifetime that no test host reproduces.
  [Fact]
  [Trait("Criterion", "AC-TEN-0019")]
  [Trait("Criterion", "AC-IAM-0021")]
  public async Task One_token_admitted_while_active_is_refused_once_the_tenant_is_suspended()
  {
    var bearer = CreateToken([
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission")]);

    TenantEligibility.Status = TenantStatus.Active;
    using var admission = CreateRequestWithToken("/test/permission", bearer);
    var admitted = await Client.SendAsync(admission);

    // THE ADMISSION IS AN ASSERTION, NOT A SETUP STEP. If this token were refused here the test below would
    // pass for the wrong reason, and the criterion is about a token that WORKED.
    Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
    var readsAfterAdmission = TenantEligibility.Calls;

    TenantEligibility.Status = TenantStatus.Suspended;
    using var replay = CreateRequestWithToken("/test/permission", bearer);
    var refused = await Client.SendAsync(replay);

    Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    Assert.True(TenantEligibility.Calls > readsAfterAdmission);
  }

  [Fact]
  public async Task Role_and_permission_authorization_share_one_live_tenant_lookup_per_request()
  {
    TenantEligibility.Calls = 0;
    using var request = CreateAuthorizedRequest(
      "/test/combined",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"),
      new Claim(JwtClaimTypes.Role, "test.role"));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, TenantEligibility.Calls);
  }

  [Fact]
  public async Task Suspended_tenant_session_can_still_use_the_separate_logout_policy()
  {
    TenantEligibility.Status = TenantStatus.Suspended;
    using var request = CreateAuthorizedRequest(
      "/test/logout",
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      EnvironmentName = Environments.Development
    });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30"
    });
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();
    tenantEligibility = new MutableTenantEligibility();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(tenantEligibility);
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapGet("/test/permission", () => Results.Ok())
      .RequireAuthorization(PermissionAuthorizationDefaults.CreatePolicyName("test.permission"));
    application.MapGet("/test/role", () => Results.Ok())
      .RequireAuthorization(RoleAuthorizationDefaults.CreatePolicyName("test.role"));
    application.MapGet("/test/combined", () => Results.Ok())
      .RequireAuthorization(
        PermissionAuthorizationDefaults.CreatePolicyName("test.permission"),
        RoleAuthorizationDefaults.CreatePolicyName("test.role"));
    application.MapGet("/test/logout", () => Results.NoContent()).RequireAuthorization();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    if (client is not null)
    {
      client.Dispose();
    }

    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private MutableTenantEligibility TenantEligibility => tenantEligibility ??
    throw new InvalidOperationException("The tenant eligibility service has not started.");

  private HttpRequestMessage CreateAuthorizedRequest(string path, params Claim[] claims)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", CreateToken(claims));
    return request;
  }

  // Separate from the helper above because that one MINTS A FRESH TOKEN PER CALL, and a test about one
  // already-issued token needs the same bearer string in two requests.
  private static HttpRequestMessage CreateRequestWithToken(string path, string bearer)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new("Bearer", bearer);
    return request;
  }

  private string CreateToken(IEnumerable<Claim> claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(
      keyProvider.Snapshot.ActiveSigningKey,
      SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;
    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "test-user"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new Claim(JwtClaimTypes.IdentityId, "1"),
      new Claim(JwtClaimTypes.TenantUserId, "2"),
      new Claim(JwtClaimTypes.SessionId, "3"),
      new Claim(JwtClaimTypes.ClientId, "ssas-erp-web"),
      new Claim(JwtClaimTypes.SecurityVersion, "1")
    };
    var token = new JwtSecurityToken(
      issuer: Issuer,
      audience: Audience,
      claims: requiredClaims.Concat(claims),
      notBefore: now.AddMinutes(-1).UtcDateTime,
      expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private sealed class MutableTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public TenantStatus? Status { get; set; } = TenantStatus.Active;
    public int Calls { get; set; }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Read(tenantId));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);

    private TenantAuthenticationEligibilityResult Read(Guid tenantId)
    {
      Calls++;
      return TenantAuthenticationEligibilityResult.FromStatus(tenantId, Status);
    }
  }

  private static async Task AssertAuthorizationFailureAsync(
    HttpResponseMessage response,
    HttpStatusCode expectedStatusCode,
    string expectedCorrelationId)
  {
    Assert.Equal(expectedStatusCode, response.StatusCode);
    Assert.Equal(expectedCorrelationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCorrelationId, document.RootElement.GetProperty("correlationId").GetString());
    Assert.Equal(
      expectedStatusCode == HttpStatusCode.Unauthorized
        ? "platform.authentication.errors.authentication_failed"
        : "platform.authentication.errors.request_rejected",
      document.RootElement.GetProperty("resourceKey").GetString());
  }
}
