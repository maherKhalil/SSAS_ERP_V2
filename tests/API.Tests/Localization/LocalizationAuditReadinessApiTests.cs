using SSAS.BuildingBlocks.Api.Transport;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Localization;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Localization;

public sealed class LocalizationAuditReadinessApiTests : IAsyncLifetime
{
  private static readonly Guid TenantId = Guid.Parse("7041080e-62af-4ac8-a90a-581335700631");
  private WebApplication? application;
  private HttpClient? client;
  private readonly State state = new();

  public static TheoryData<string, object> MutationRequests => new()
  {
    {
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "candidate-do-not-echo", expectedRowVersion = (string?)null }
    },
    {
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo",
      new { targetVersionNumber = 1, expectedRowVersion = "AQIDBAUGBwg=" }
    },
    {
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default",
      new { expectedRowVersion = "AQIDBAUGBwg=" }
    }
  };

  // ⚠⚠⚠ THE BEHAVIOURAL HALF OF `AC-LOC-0064`, AND IT COMPLETES A PAIR.
  //
  // *"An otherwise authorized Production localization mutation proceeds only when audit readiness succeeds;
  // otherwise it returns HTTP 503 `localization.audit_readiness_unavailable` with no SQL state change,
  // Domain event, cache eviction, submitted-text logging, or internal-cause disclosure."*
  //
  // **`LocalizationArchitectureTests.Every_localization_mutation_handler_retains_locked_tenant_eligibility_
  // and_audit_readiness` carries the WIRING half** — that all four handlers call the guard — **and it is a
  // SOURCE-TEXT match, so it proves the call is WRITTEN and not that it is REACHED or honoured.** This test
  // is the other half: the call runs, the refusal happens, and the response is what the criterion says.
  //
  // WHAT THIS ONE ASSERTS, five of the criterion's elements:
  //
  //   the 503 · the exact code `localization.audit_readiness_unavailable`
  //   NO SQL STATE CHANGE — `RepositoryCalls` and `SaveCalls` both zero, not merely a status check
  //   no submitted-text disclosure — the candidate value is absent from the body
  //   no internal-cause disclosure — the provider's exception message is absent
  //
  // ⚠⚠ WHAT NEITHER HALF ASSERTS: **no Domain event and no cache eviction.** This fixture counts repository
  // and save calls; it has no dispatcher and no cache to inspect.
  //
  // ⚠⚠⚠ CORRECTED ONE PASS LATER, AND THE CORRECTION IS THE USEFUL PART. *No Domain event* IS carried —
  // `PlatformLocalizationSqlServerTests.Audit_unavailable_leaves_all_localization_sql_state_and_events_
  // unchanged` runs all four mutations against a real database with a `RecordingDomainEventDispatcher` and
  // asserts the event count is unmoved. **I had recorded it as carried by nothing, bounded to this pair,
  // and the instrument existed in a suite I had not examined.** Not missing — UNSEARCHED, which is the same
  // mistake as reading an architecture pass's leftovers as a coverage gap.
  //
  // ⚠⚠⚠ AND *NO CACHE EVICTION* DID NOT SURVIVE EITHER — CORRECTED AGAIN, ONE PASS LATER.
  //
  // I searched `tests` with no cap for `EvictTenant`, `ILocalizationTenantCache` and any recording cache,
  // found only passthroughs, and concluded the observable did not exist. **THE SEARCH WAS SHAPED FOR THE
  // WRONG KIND OF INSTRUMENT.** `LocalizationResolverTests.Post_commit_domain_event_evicts_the_tenant_
  // generation` observes eviction BEHAVIOURALLY — after the event is handled, a re-resolve returns the new
  // text and the override reader's call count has risen. No spy, no recording double, nothing my search
  // could have matched.
  //
  // **So the technique for asserting *no eviction here* exists and is demonstrated in that file**: run the
  // refused mutation, re-resolve, and assert the reader was NOT called again. It is fixture work, not an
  // impossibility.
  //
  // ⚠⚠ BOTH RESIDUALS WERE UNSEARCHED, NOT UNINSTRUMENTED. The domain-event one was in a suite I had not
  // read; this one was behind an instrument shape I had not imagined. **A missing-instrument explanation
  // was offered for both and neither survived.** What generalised was the search, not the mechanism.
  //
  // ⚠ `MemberData` here is a hand-written list, but the name quantifies nothing — *an authorized mutation*,
  // not *every route* — so it claims no population and `B20` does not apply. Checked, not assumed.
  [Theory]
  [MemberData(nameof(MutationRequests))]
  [Trait("Criterion", "AC-LOC-0064")]
  public async Task Authorized_active_mutation_returns_safe_503_when_audit_is_unavailable(string path, object body)
  {
    state.Reset();
    using var request = AuthorizedRequest(path, body);

    var response = await Client.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Contains("localization.audit_readiness_unavailable", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("candidate-do-not-echo", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("provider-secret-reason", responseBody, StringComparison.Ordinal);
    Assert.Equal(1, state.ReadinessCalls);
    Assert.Equal(0, state.RepositoryCalls);
    Assert.Equal(0, state.SaveCalls);
  }

  // ⚠⚠⚠ EXAMINED FOR `AC-LOC-0017` AND **NOT CITED**, BECAUSE THE TOKEN IS EMPTY RATHER THAN WRONG.
  //
  // *"View, Manage, and ViewHistory grant ONLY THEIR EXACT OPERATIONS under trusted live Active Tenant
  // scope."* The discriminating content of *only their exact* is OVER-GRANTING — a permission that opens
  // an operation it should not. **`includeManagePermission: false` sends NO `X-Test-Permission` header at
  // all** (`:382-385`), so this is *no permission ⇒ refused*, which is a weaker and different claim. **An
  // empty token cannot detect over-granting: every permission over-grants equally when the caller holds
  // none.**
  //
  // ⚠ THE FIXTURE THAT WOULD CARRY IT, NAMED SO THE GAP IS CONCRETE: a token holding `ViewLocalization`
  // sent to one of these three mutation routes, refused. And for the other direction, `ManageLocalization`
  // sent to the history route.
  //
  // ⚠⚠ SEARCHED BY MECHANISM, BECAUSE THE CLAIM IS AN ABSENCE. To exercise over-granting a test must SEND
  // one of those permissions; the only channel is the `X-Test-Permission` header. `ViewLocalization` and
  // `ViewLocalizationHistory` across all of `tests/` appear in exactly two places — the ROUTE INVENTORY,
  // which declares which permission each route requires, and the PERMISSION CATALOG test, which asserts the
  // names exist. **Neither sends one. No test in the repository presents a View or ViewHistory token to any
  // route.**
  //
  // **So the criterion's *exact* is held structurally — the inventory pins each route to its own permission
  // and the shared pipeline enforces policies — and behaviourally by nothing for localization.** That is
  // the declaration/realisation split again, and the reason this test is a poor citation for it: it is a
  // real assertion about a different proposition.
  // ⚠⚠⚠ AND THE PROPER HOME IS `AC-LOC-0052`, WHICH SPLITS THE VERY DISTINCTION `0017` COLLAPSES.
  // *"MISSING/wrong permission, ordinary user, anonymous management, Preview/Undo/Restore WITHOUT MANAGE,
  // and history without ViewHistory are DENIED."* **`Missing` and `without Manage` are exactly what an
  // empty token tests**, and the three rows of `MutationRequests` are the PUT, the Undo and the
  // Restore-Default. Cited for those clauses.
  //
  // **The criterion separates *missing* from *wrong* in one phrase; `AC-LOC-0017` does not, which is why
  // this test belongs to one and not the other.** Not carried from that sentence: *wrong permission*,
  // *ordinary user*, *anonymous management*, *Preview* (not among these three routes), and *history
  // without ViewHistory*.
  //
  // ⚠ `ReadinessCalls == 0` IS AN ORDERING CLAIM ON TOP OF THE DENIAL: authorization runs before the audit
  // gate, so a caller without Manage cannot probe whether audit readiness is degraded. A route that
  // checked readiness first would answer `503` to an unauthorized caller and leak operational state.
  [Theory]
  [MemberData(nameof(MutationRequests))]
  [Trait("Criterion", "AC-LOC-0052")]
  public async Task Missing_manage_permission_returns_403_before_audit_readiness(string path, object body)
  {
    state.Reset();
    using var request = AuthorizedRequest(path, body, includeManagePermission: false);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(0, state.ReadinessCalls);
  }

  // ⚠ CITES THE *"current live Tenant"* CLAUSE OF `AC-LOC-0044` (PUT), `AC-LOC-0045` (Undo) AND
  // `AC-LOC-0046` (Restore-Default) — THREE IDS FOR THE THREE ROWS OF `MutationRequests`, and the mapping is
  // one-to-one rather than approximate. A suspended tenant reaches each of the three real routes and gets
  // 403; nothing else in either sentence is asserted here.
  //
  // ⚠⚠ THIS IS THE ROUTE-LAYER LEG THAT `RequestTenantEligibilityTests` DELIBERATELY DOES NOT CARRY. That
  // file proves the eligibility MECHANISM computes liveness correctly and touches no endpoint; a clause
  // saying a ROUTE ENFORCES liveness needs a route. **`ReadinessCalls` staying at 0 is the ordering half —
  // the tenant check runs BEFORE audit readiness, so a suspended tenant cannot probe audit state.**
  //
  // ⚠⚠⚠ ONE STATUS IS ENOUGH HERE, AND THE REASON IS COMPOSITION — CHECKED, NOT ASSUMED.
  //
  // This drives only `Suspended`, while `AuthorizationPipelineTests.Non_active_or_missing_tenant_is_rejected`
  // drives all three non-Active statuses plus `null` against a TEST route. That reads like neither location
  // covering both axes. **It is not, because both exercise THE SAME COMPONENT:**
  //
  //   `InitializeAsync` calls `AddHostPermissionAuthorization()` — the same call `SSAS.Host.API/Program.cs`
  //   makes — which registers `LiveTenantEligibilityAuthorization` over the REAL `RequestTenantEligibility`.
  //   `PermissionAuthorizationHandler` and `RoleAuthorizationHandler` each take it as a CONSTRUCTOR
  //   DEPENDENCY, so it is consulted rather than merely registered, and the localization routes reach it by
  //   `.RequireAuthorization($"Permission:{ManageLocalization}")`.
  //
  // **So the pipeline test carries the STATUS POPULATION and this test is the WITNESS THAT THESE ROUTES ARE
  // WIRED TO IT — one status suffices for a wiring claim.**
  //
  // ⚠⚠ BUT THE COMPOSITION COVERS SEVEN ROUTES, NOT NINE, AND THE TWO IT MISSES ARE GATED DIFFERENTLY.
  // `MapPlatformLocalizationEndpoints` gives the effective group a BARE `.RequireAuthorization()` — the
  // default policy, which carries only `DenyAnonymousAuthorizationRequirement`, so neither permission handler
  // is invoked and `LiveTenantEligibilityAuthorization` never runs for `/effective` or `/effective/batch`.
  // Their liveness check is at the QUERY-HANDLER layer instead (`GetTenantLocalizationResourceQueryHandler`
  // and friends call `eligibility.GetEligibilityAsync` and return `TenantIneligible`), and it is
  // `LocalizationEffectiveApiTests` that covers them — with its own enumerated status list and its own
  // recorded `B20` note. **Two mechanisms, two test sites; the composition argument applies to one of them.**
  [Theory]
  [MemberData(nameof(MutationRequests))]
  [Trait("Criterion", "AC-LOC-0044")]
  [Trait("Criterion", "AC-LOC-0045")]
  [Trait("Criterion", "AC-LOC-0046")]
  public async Task Inactive_tenant_returns_403_without_disclosing_audit_state(string path, object body)
  {
    state.Reset();
    state.TenantStatus = TenantStatus.Suspended;
    using var request = AuthorizedRequest(path, body);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(0, state.ReadinessCalls);
  }

  [Fact]
  public async Task Audit_provider_exception_returns_safe_503_without_internal_reason()
  {
    state.Reset();
    state.ReadinessException = new InvalidOperationException("provider-secret-reason");
    using var request = AuthorizedRequest(
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "candidate-do-not-echo", expectedRowVersion = (string?)null });

    var response = await Client.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.DoesNotContain("provider-secret-reason", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("candidate-do-not-echo", responseBody, StringComparison.Ordinal);
  }

  // ==================================================================================================
  // ⚠⚠⚠ THE WRONG PERMISSION, WHICH THREE CRITERIA ASK FOR AND NOTHING IN THIS REPOSITORY PRESENTED.
  // ==================================================================================================
  //
  // Until this test, **no test anywhere sent a `ViewLocalization` or `ViewLocalizationHistory` token to any
  // route.** Both names appeared in exactly two places — the route inventory, which DECLARES which
  // permission each route requires, and the permission catalog, which asserts the names EXIST. Neither
  // sends one. The nearest thing was `Missing_manage_permission_…` above, which sends NO permission at all,
  // and **an empty token cannot discriminate among non-empty ones: every permission over-grants equally
  // when the caller holds none.**
  //
  // ---- ONE FIXTURE, THREE CRITERIA, AND EACH ROW CARRIES A DIFFERENT CLAUSE.
  //
  //   `AC-LOC-0017`  *"View, Manage, and ViewHistory grant ONLY THEIR EXACT OPERATIONS."*  — all four rows
  //   `AC-LOC-0051`  *"Each exact permission succeeds ONLY for its documented operations."* — the *only*
  //                  half, which the two success tests below cannot reach: they show Manage succeeding AT
  //                  its operations, never failing elsewhere
  //   `AC-LOC-0052`  *"WRONG permission … and HISTORY WITHOUT VIEWHISTORY are denied."* — rows 1-2 are
  //                  *wrong permission*; rows 3-4 are *history without ViewHistory*
  //
  // ⚠⚠ ROW 4 IS THE ONE THE PACKAGE ARGUES FOR EXPLICITLY, AND IT IS NOT AN OBVIOUS CASE.
  // `PlatformLocalizationRouteInventoryTests:60-61` records the reason: *"Reading a resource and reading
  // its history are SEPARATE GRANTS, because history exposes prior override VALUES and therefore prior
  // business wording."* **So `ViewLocalization` — the permission for reading the resource — must NOT open
  // the history of that same resource.** A reader who thought *View covers reading* would merge them, and
  // until now nothing would have objected.
  //
  // ⚠ ROW 3 IS THE MIRROR AND IT MATTERS FOR A DIFFERENT REASON: `ManageLocalization` is the STRONGEST
  // localization grant, and it still does not open history. **A permission model where the write grant
  // implies every read grant is the ordinary shape**, and this package deliberately does not have it.
  //
  // ---- WHAT THE COUNTERS ADD BEYOND THE STATUS.
  //
  // `RepositoryCalls == 0` on every row: the refusal happens before any handler touches storage. A route
  // that authorized loosely and then filtered would answer `403` from inside the handler and satisfy a
  // status-only assertion while having already read the tenant's data.
  [Theory]
  [InlineData("ViewLocalization on a write", PlatformPermissionNames.ViewLocalization, false)]
  [InlineData("ViewLocalizationHistory on a write", PlatformPermissionNames.ViewLocalizationHistory, false)]
  [InlineData("ManageLocalization on history", PlatformPermissionNames.ManageLocalization, true)]
  [InlineData("ViewLocalization on history", PlatformPermissionNames.ViewLocalization, true)]
  [Trait("Criterion", "AC-LOC-0017")]
  [Trait("Criterion", "AC-LOC-0051")]
  [Trait("Criterion", "AC-LOC-0052")]
  public async Task A_permission_does_not_open_an_operation_it_does_not_document(
    string because, string permission, bool history)
  {
    state.Reset();

    using var request = history
      ? new HttpRequestMessage(
        HttpMethod.Get, "/api/platform/localization/resources/platform.common.actions.save/history")
      : new HttpRequestMessage(
        HttpMethod.Put, "/api/platform/localization/resources/platform.common.actions.save/overrides/en")
      {
        Content = JsonContent.Create(new { value = "Store", expectedRowVersion = (string?)null })
      };

    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    request.Headers.Add("X-Test-Permission", permission);

    var response = await Client.SendAsync(request);

    // `because` names the pair in the failure text. A theory over four permission/operation combinations
    // reports only its `[InlineData]` values otherwise, and `"Platform.Localization.View", false` does not
    // say which grant was expected to stay shut.
    Assert.True(
      response.StatusCode == HttpStatusCode.Forbidden,
      $"{because}: expected 403, got {(int)response.StatusCode}. That permission opened an operation it "
      + "does not document.");
    Assert.Equal(0, state.RepositoryCalls);
    Assert.Equal(0, state.SaveCalls);
  }

  // ⚠ CITES `AC-LOC-0051`'s *SUCCEEDS* HALF — *"Each exact permission SUCCEEDS only for its documented
  // operations."* A `ManageLocalization` token reaches the create route and gets `Created`, with
  // `SaveCalls == 1` and `Added` non-null so the success is a WRITE rather than a status.
  //
  // ⚠⚠ NOT THE WORD *ONLY*. That is the over-granting direction — Manage succeeding at something it should
  // not — and it is the same half `AC-LOC-0017` needs and nothing supplies. **`0051` and `0052` are the
  // specification's own positives/negatives pair, and between them they still leave *only* unwitnessed:
  // `0052` covers denials for callers who lack a permission, `0051` covers successes for callers who hold
  // the right one, and neither presents the WRONG one.**
  //
  // ⚠⚠⚠ THIS IS ALSO THE ANTI-VACUITY CONTROL FOR EVERY `403` AND `503` IN THIS FILE, AND IT IS THE ONLY
  // ONE. Without a mutation that actually completes, a host that refused every request — wrong policy,
  // broken route, a gate stuck closed — satisfies the audit-unavailable theory, the missing-permission
  // theory and the inactive-tenant theory together. **Three refusal families, one positive.**
  [Fact]
  [Trait("Criterion", "AC-LOC-0051")]
  public async Task Ready_authorized_create_continues_to_the_existing_success_contract()
  {
    state.Reset();
    state.IsReady = true;
    using var request = AuthorizedRequest(
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "Store", expectedRowVersion = (string?)null });

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal(1, state.ReadinessCalls);
    Assert.Equal(1, state.SaveCalls);
    Assert.NotNull(state.Added);
  }

  // ⚠ CITES `AC-LOC-0051`'s *SUCCEEDS* HALF FOR THE OTHER TWO OPERATIONS. The create route is covered
  // above; *documented operations* is plural, and Undo and Restore-Default are separately documented and
  // separately gated. **A permission that opened create and not undo would satisfy the test above
  // completely.**
  //
  // ⚠⚠ AND THIS PAIRS ROW-FOR-ROW WITH `Missing_manage_permission_returns_403_before_audit_readiness`,
  // WHICH IS WHAT MAKES EITHER MEAN ANYTHING. The same three routes appear with Manage (here and above,
  // succeeding) and without it (there, refused). **Same routes, same bodies, one variable — so the `403`
  // is attributable to the permission and not to the route, the body or the fixture.**
  [Theory]
  [InlineData("/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo", true)]
  [InlineData("/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default", false)]
  [Trait("Criterion", "AC-LOC-0051")]
  public async Task Ready_authorized_existing_mutation_routes_continue_past_the_audit_gate(string path, bool undo)
  {
    state.Reset();
    state.IsReady = true;
    var body = undo
      ? (object)new { targetVersionNumber = 1, expectedRowVersion = "AQIDBAUGBwg=" }
      : new { expectedRowVersion = "AQIDBAUGBwg=" };
    using var request = AuthorizedRequest(path, body);

    var response = await Client.SendAsync(request);
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("localization.override_missing", document.RootElement.GetProperty("code").GetString());
    Assert.Equal(1, state.ReadinessCalls);
    Assert.True(state.RepositoryCalls > 0);
  }

  [Fact]
  // ⚠ CITES THE FIRST CLAUSE OF `AC-LOC-0018` — *"Strict DTOs REJECT unknown/TenantId fields and bounded
  // projections disclose no foreign state."* Four malformations, each answered `400`, and the first is a
  // FORGED `tenantId` on a route whose contract has no such property.
  //
  // ⚠⚠ AND IT PROVES MORE THAN THE WORD *REJECT*: `ReadinessCalls`, `RepositoryCalls` and `SaveCalls` are
  // all asserted ZERO, so the refusal happens BEFORE any handler runs. A DTO that bound the field and then
  // ignored it would answer `400` from somewhere further in and satisfy a status-only assertion.
  //
  // ⚠⚠⚠ THIS IS WHY `LocalizationArchitectureTests.Localization_commands_never_accept_tenant_or_actor_
  // identity` WAS DELIBERATELY LEFT UNCITED FOR THIS CRITERION. That test asserts the property DOES NOT
  // EXIST — a structural absence, and a precondition for rejection rather than rejection itself. **The verb
  // is asserted HERE, by a different mechanism, at a different layer.** Two real claims; one criterion
  // clause; only one of them is what the clause says.
  //
  // ⚠ NOT CLAUSE 2. *Bounded projections disclose no foreign state* is about what a READ returns and is
  // untouched by anything in this test.
  [Trait("Criterion", "AC-LOC-0018")]
  public async Task Shared_strict_json_binding_rejects_unknown_duplicate_missing_and_wrong_typed_fields_before_handlers()
  {
    var requests = new[]
    {
      (HttpMethod.Put, "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
        "{\"value\":\"Store\",\"expectedRowVersion\":null,\"tenantId\":\"forged\"}"),
      (HttpMethod.Post, "/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo",
        "{\"targetVersionNumber\":1,\"targetVersionNumber\":2,\"expectedRowVersion\":\"AQIDBAUGBwg=\"}"),
      (HttpMethod.Post, "/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default", "{}"),
      (HttpMethod.Post, "/api/platform/localization/preview",
        "{\"resourceKey\":\"platform.common.actions.save\",\"culture\":\"en\",\"value\":7}")
    };

    foreach (var (method, path, body) in requests)
    {
      state.Reset();
      using var request = AuthorizedRawRequest(method, path, body);
      using var response = await Client.SendAsync(request);

      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Equal(0, state.ReadinessCalls);
      Assert.Equal(0, state.RepositoryCalls);
      Assert.Equal(0, state.SaveCalls);
    }
  }

  // ⚠ CITES `AC-LOC-0061`'s LAST CLAUSE — *"…and maps ONLY VALID STALE VALUES to 409 `concurrency.conflict`"*
  // — and it is the *only* leg in the codebase that carries the WORD ONLY. `Error_mapper_maps_internal_
  // concurrency_to_http_contract` shows a stale value reaching 409; that is the permissive half. **This shows
  // a NONCANONICAL value NOT reaching it**: `AQIDBAUGBwg_` returns 400 with `localization.rowversion_invalid`
  // and `RepositoryCalls` stays at 0, so the concurrency comparison is never performed at all.
  //
  // ⚠⚠ IT IS ALSO THE JOIN THE OTHER TWO CITERS DEPEND ON. `RowVersionCodecTests` proves a shared
  // BuildingBlocks codec refuses Base64Url, and `LocalizationTransportContractTests` proves a static mapper
  // holds 400 plus the code — **neither touches a localization route.** This drives the real PUT endpoint and
  // is what makes those citations something other than ADJACENT-SCOPE.
  //
  // ⚠⚠⚠ THE RESIDUAL, AND IT IS THE *ONLY* THAT SURVIVES: this pins one refused value out of the seven
  // categories 0061 names. The remaining six are refused at the codec and are NOT observed through HTTP —
  // ORDERED CHECKS AGAIN, since one input can only ever demonstrate one refusal.
  [Fact]
  [Trait("Criterion", "AC-LOC-0061")]
  public async Task Malformed_transport_rowversion_is_400_before_concurrency_or_audit_processing()
  {
    state.Reset();
    using var request = AuthorizedRequest(
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "candidate-do-not-echo", expectedRowVersion = "AQIDBAUGBwg_" });

    using var response = await Client.SendAsync(request);
    var responseText = await response.Content.ReadAsStringAsync();
    using var problem = JsonDocument.Parse(responseText);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("localization.rowversion_invalid", problem.RootElement.GetProperty("code").GetString());
    Assert.DoesNotContain("candidate-do-not-echo", responseText, StringComparison.Ordinal);
    Assert.Equal(0, state.ReadinessCalls);
    Assert.Equal(0, state.RepositoryCalls);
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
    builder.WebHost.UseTestServer();
    builder.Services.AddHttpContextAccessor();
    builder.Services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddScheme<AuthenticationSchemeOptions, TestBearerHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
    builder.Services.AddHostPermissionAuthorization();
    builder.Services.AddSingleton(state);
    builder.Services.AddSingleton<ICurrentTenant>(new CurrentTenant(TenantId));
    builder.Services.AddSingleton<ICurrentUser, CurrentUser>();
    builder.Services.AddSingleton<IDateTimeProvider, Clock>();
    builder.Services.AddSingleton<ILocalizationCatalog>(GeneratedLocalizationCatalog.Instance);
    builder.Services.AddScoped<ITenantAuthenticationEligibilityReadService, Eligibility>();
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddScoped<ILocalizationManagementAuditReadiness, AuditReadiness>();
    builder.Services.AddScoped<ITenantLocalizationSettingsRepository, SettingsRepository>();
    builder.Services.AddScoped<ITenantLocalizationOverrideRepository, OverrideRepository>();
    builder.Services.AddScoped<IPlatformUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<CreateTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<UpdateTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<UndoTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<RestoreTenantLocalizationDefaultCommandHandler>();
    builder.Services.AddScoped<PreviewTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<ListTenantLocalizationResourcesQueryHandler>();
    builder.Services.AddScoped<GetTenantLocalizationResourceQueryHandler>();
    builder.Services.AddScoped<GetTenantLocalizationHistoryQueryHandler>();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformLocalizationEndpoints();
    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host is unavailable.");

  private static HttpRequestMessage AuthorizedRequest(string path, object body, bool includeManagePermission = true)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
    if (!path.EndsWith("/undo", StringComparison.Ordinal) && !path.EndsWith("/restore-default", StringComparison.Ordinal))
    {
      request.Method = HttpMethod.Put;
    }
    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    if (includeManagePermission)
    {
      request.Headers.Add("X-Test-Permission", PlatformPermissionNames.ManageLocalization);
    }
    return request;
  }

  private static HttpRequestMessage AuthorizedRawRequest(HttpMethod method, string path, string body)
  {
    var request = new HttpRequestMessage(method, path)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    request.Headers.Add("X-Test-Permission", PlatformPermissionNames.ManageLocalization);
    return request;
  }

  private sealed class TestBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
  {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      if (!Request.Headers.TryGetValue("X-Test-Tenant", out var tenant))
      {
        return Task.FromResult(AuthenticateResult.NoResult());
      }
      var claims = new List<Claim>
      {
        new(JwtClaimTypes.Subject, "audit-api-user"),
        new(JwtClaimTypes.TenantId, tenant.ToString())
      };
      if (Request.Headers.TryGetValue("X-Test-Permission", out var permission))
      {
        claims.Add(new Claim(JwtClaimTypes.Permission, permission.ToString()));
      }
      var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
      return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
  }

  private sealed class State
  {
    public TenantStatus? TenantStatus { get; set; }
    public bool IsReady { get; set; }
    public Exception? ReadinessException { get; set; }
    public int ReadinessCalls { get; set; }
    public int RepositoryCalls { get; set; }
    public int SaveCalls { get; set; }
    public TenantLocalizationOverride? Added { get; set; }

    public void Reset()
    {
      TenantStatus = SSAS.Platform.Domain.Enums.TenantStatus.Active;
      IsReady = false;
      ReadinessException = null;
      ReadinessCalls = 0;
      RepositoryCalls = 0;
      SaveCalls = 0;
      Added = null;
    }
  }

  private sealed class Eligibility(State state) : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, state.TenantStatus));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class AuditReadiness(State state) : ILocalizationManagementAuditReadiness
  {
    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
      state.ReadinessCalls++;
      if (state.ReadinessException is not null)
      {
        return Task.FromException<LocalizationManagementAuditReadinessResult>(state.ReadinessException);
      }
      return Task.FromResult(state.IsReady
        ? LocalizationManagementAuditReadinessResult.Ready
        : LocalizationManagementAuditReadinessResult.Unavailable);
    }
  }

  private sealed class SettingsRepository(State state) : ITenantLocalizationSettingsRepository
  {
    private readonly TenantLocalizationSettings settings = TenantLocalizationSettings.Create(TenantId, LocalizationCulture.English);
    public Task<TenantLocalizationSettings?> GetForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult<TenantLocalizationSettings?>(settings);
    }
    public Task<TenantLocalizationSettings> GetOrCreateForUpdateAsync(Guid tenantId, LocalizationCulture defaultCulture, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult(settings);
    }
  }

  private sealed class OverrideRepository(State state) : ITenantLocalizationOverrideRepository
  {
    public Task<TenantLocalizationOverride?> GetForUpdateAsync(Guid tenantId, ResourceKey resourceKey, LocalizationCulture culture, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult<TenantLocalizationOverride?>(null);
    }
    public Task<LocalizationVersionSnapshot?> GetVersionSnapshotAsync(Guid overrideId, TenantOverrideVersion versionNumber, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult<LocalizationVersionSnapshot?>(null);
    }
    public Task AddAsync(TenantLocalizationOverride localizationOverride, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      typeof(TenantLocalizationOverride).GetProperty(nameof(TenantLocalizationOverride.RowVersion))!
        .SetValue(localizationOverride, new byte[RowVersionCodec.SqlServerRowVersionLength]);
      state.Added = localizationOverride;
      return Task.CompletedTask;
    }
  }

  private sealed class UnitOfWork(State state) : IPlatformUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      state.SaveCalls++;
      return Task.FromResult(Result.Success(1));
    }
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new Transaction());
  }

  private sealed class Transaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class CurrentTenant(Guid tenantId) : ICurrentTenant { public Guid? TenantId { get; } = tenantId; }
  private sealed class CurrentUser : ICurrentUser
  {
    public string? UserId => "audit-api-user";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }
  private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
