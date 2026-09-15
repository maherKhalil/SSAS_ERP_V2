using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAS.API.Tests.Infrastructure;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.API.Tests.Localization;

// ==================================================================================================
// FP-004's UNCITED ROUTE AND TRANSPORT CRITERIA, CENSUSED 2026-09-05. NONE IS CITED, AND ONE OF THEM
// IS THE ONLY GENUINE HOLE THE CENSUS FOUND.
// ==================================================================================================
//
// **The resolver-layer cells are recorded in `LocalizationResolverTests`, which also states the placement
// convention these follow.** *Nothing here is a citation.*
//
// ---- ⚠⚠⚠ `AC-LOC-0033`: A SPECIFIED REFUSAL THAT NO TEST HAS EVER ASSERTED.
//
// *"Null expectedRowVersion creates only when absent; existing/inactive returns 409
// `localization.override_already_exists`."*
//
// ***THAT ERROR CODE APPEARS IN ZERO TEST FILES IN THE REPOSITORY.*** Five files hold it: the domain error,
// the API mapper, an architecture reachability guard, and two documents. ⚠ **And the grep crosses the mapping
// boundary cleanly — checked first, because a token that crosses a mapper usually changes its name: the
// domain's `"localization.override_already_exists"` maps to the API's `"localization.override_already_exists"`,
// identical, so a renamed downstream spelling is not hiding a witness.**
//
// **Its three sibling codes ARE asserted** — `undo_not_available` and `override_already_default` in
// `LocalizationTransportContractTests` under `AC-LOC-0035`, and `override_missing` once, incidentally, in
// `LocalizationAuditReadinessApiTests`. ***FOUR CODES, ONE UNTESTED, AND IT IS THE CREATE-COLLISION ONE.***
//
// ⚠ **BOUND, UNDISCHARGED: some test may assert that a duplicate create FAILS without asserting the code.
// What is established is that the specified 409 is asserted nowhere.** `AC-LOC-0034` is the weaker sibling —
// its `override_missing` half is witnessed only incidentally, in a file about audit readiness.
//
// ---- ⚠⚠ `AC-LOC-0047` AND `AC-LOC-0048`: ONE ASSERTION LICENSES FOUR CRITERIA AND CARRIES TWO TRAITS.
//
// `Generated_document_exposes_exactly_the_nine_approved_localization_routes_and_authentication` below cites
// **the *"exact OpenAPI"* clause** of `AC-LOC-0042` and `AC-LOC-0043`, and its comment is scrupulous about
// the nine clauses it does not assert.
//
// ***BUT `AC-LOC-0047` (history) ENDS "…and OpenAPI" AND `AC-LOC-0048` (Preview) ENDS "…and OpenAPI", AND
// BOTH ROUTES ARE AMONG THE NINE THE SAME ASSERTION PINS*** — `:172` reads the preview path's description by
// name. **Identical clause, identical witness, no trait.**
//
// ⚠ **THIS IS NOT SLOPPINESS AND SAYING SO MATTERS.** The citation is disciplined and discloses its own
// limits in prose. **What did not happen is that the same disclosure was carried across to the other two
// criteria the same assertion reaches.** ***Whichever policy is right — cite a clause and disclose, or refuse
// a one-of-nine citation — the tree is applying BOTH to ONE assertion.*** *That is an owner-facing
// consistency question, not a missing test, and adding two traits here would settle it by fait accompli.*
//
// ---- THE SQL CELLS, FOR COMPLETENESS OF THE TWENTY.
//
// `AC-LOC-0055` (SQL uniqueness/coherence) is observed inside
// `PlatformLocalizationSqlServerTests.Aggregate_and_history_enforce_coherence_uniqueness_fingerprints_and_
// immutability`, which carries `AC-LOC-0029`. `AC-LOC-0056` names **five** concurrent operations —
// create, update, Undo, Restore, settings initialisation — and **only create and settings-initialisation
// have concurrency tests**; update, Undo and Restore have none. *Two of five would read as five.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class LocalizationOpenApiContractTests(HostWebApplicationFactory factory)
{
  // ⚠⚠⚠ ONE POPULATION FOR BOTH ROUTE TESTS, AND THE DELIVERABLE IS PROPAGATION RATHER THAN DETECTION.
  //
  // These nine templates were previously written TWICE — once here as templates for the document
  // comparison, and once in the authentication test as CONCRETE paths with `{resourceKey}` and `{culture}`
  // already substituted. Only the document comparison is closed against reality, and that asymmetry is the
  // defect: a tenth route reddens the document test, whoever repairs THAT list has discharged the alarm,
  // and nothing anywhere points at the authentication list still holding nine.
  //
  // ⚠⚠ A PARTIAL ALARM IS WORSE THAN NO ALARM, BECAUSE IT CONSUMES THE ATTENTION THE WHOLE PROBLEM NEEDED.
  // With no alarm at all, two stale lists might eventually be swept together. With one, the sweep stops the
  // moment the red goes green.
  //
  // ⚠ BE PRECISE ABOUT WHAT THIS FIXES, BECAUSE IT IS NOT WHAT IT SOUNDS LIKE: the authentication test still
  // loops over whatever this static says, so a tenth ROUTE still does not redden it directly. What changes
  // is that the document test's red is now REPAIRED IN ONE PLACE and the repair PROPAGATES to the
  // authentication loop. The derivation FORECLOSES the drift where a cross-check would only DETECT it.
  //
  // ⚠⚠⚠ PLANTED WITH A REAL TENTH ROUTE — `group.MapGet("/{resourceKey}/planted-tenth-route", …)` added to
  // `LocalizationEndpointRouteBuilderExtensions`, run, removed — BECAUSE A TENTH ENTRY IN THIS STATIC WOULD
  // HAVE TESTED THE WRONG DIRECTION. Adding to the static reddens both tests because both read the static;
  // that confirms COUPLING and says nothing about PRODUCT DRIFT, which is what actually goes wrong.
  //
  //   tenth route, static untouched  → document test FAILED (ten paths against nine).
  //                                    Authentication test PASSED, over NINE. **That is the defect: the
  //                                    alarm fires and the tenth route's gate is never exercised.**
  //   tenth route, static updated    → both PASSED, and the request log shows TEN `401` responses
  //                                    including `…/planted-tenth-route`. **That is the repair
  //                                    propagating, and NO RED WOULD EVER HAVE TOLD ME IT HAPPENED —
  //                                    the second half of this plant is an OBSERVATION, not a colour.**
  private static readonly (HttpMethod Method, string Template)[] ApprovedRoutes =
  [
    (HttpMethod.Get, "/api/platform/localization/resources"),
    (HttpMethod.Get, "/api/platform/localization/resources/{resourceKey}"),
    (HttpMethod.Put, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}"),
    (HttpMethod.Post, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo"),
    (HttpMethod.Post, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default"),
    (HttpMethod.Get, "/api/platform/localization/resources/{resourceKey}/history"),
    (HttpMethod.Post, "/api/platform/localization/preview"),
    (HttpMethod.Get, "/api/platform/localization/effective"),
    (HttpMethod.Post, "/api/platform/localization/effective/batch")
  ];

  // The substitution that turns a documented template into a routable path.
  //
  // ⚠⚠⚠ I PREDICTED THAT A WRONG SUBSTITUTION WOULD FAIL LOUDLY WITH A 404. **MEASURED, AND IT DOES NOT
  // FAIL AT ALL.** Planting `{culture}` → `xx-BROKEN` left all three tests GREEN: every one of the nine
  // requests still routed and still returned 401, because **authentication runs before a route parameter
  // means anything**, and these segments carry no constraint. A literal unsubstituted `{culture}` would
  // route just as happily.
  //
  // ⚠⚠ SO THE SUBSTITUTION CARRIES NO WEIGHT IN WHAT THE TEST MEASURES — the claim is about route PATTERNS
  // and the parameter values are arbitrary — but *carries no weight* is not *cannot rot*: rename a template
  // placeholder and `Concrete` silently passes the braces through, leaving a test that reads as though it
  // exercises a real resource key while asserting nothing about one. The guard below is therefore a
  // LEGIBILITY control rather than a routing one, and it is planted: with the `{culture}` replacement
  // removed it fails and names the offending template.
  private static string Concrete(string template)
  {
    var path = template
      .Replace("{resourceKey}", "platform.common.actions.save", StringComparison.Ordinal)
      .Replace("{culture}", "en", StringComparison.Ordinal);

    Assert.False(path.Contains('{', StringComparison.Ordinal),
      $"'{template}' still holds an unsubstituted placeholder, so '{path}' is not a concrete path. A "
      + "template placeholder was renamed and `Concrete` was not updated to match.");
    return path;
  }

  // ⚠ CITES `AC-LOC-0062`'s FIRST CLAUSE — *"All nine M2 routes are NON-ANONYMOUS"* — AT THE OTHER LAYER.
  // `PlatformLocalizationRouteInventoryTests` already cites this clause and asserts `HasAuthorization`, i.e.
  // that the METADATA IS DECLARED. This asserts the OBSERVABLE: an anonymous request to each of the nine
  // returns 401, with the no-store/no-cache/no-referrer/nosniff headers and the failure resource key.
  // **Declared and enforced are different claims and this is the second one**; neither test subsumes the
  // other, and a route could carry `[Authorize]` metadata that some later middleware never honours.
  //
  // ⚠⚠ RESIDUAL, AND IT SURVIVES THE `ApprovedRoutes` DERIVATION ABOVE — WHICH IS WHY THIS STILL DOES NOT
  // CARRY `AC-LOC-0053` ("Milestone 2 exposes no anonymous localization HTTP route"). Sharing the static
  // removed the DRIFT between the two lists; it did not make this loop's population closed. **A tenth route
  // still does not redden THIS test** — it reddens the document test, and the repair then propagates here.
  // The claim is therefore *each of the routes we approved refuses anonymous callers*, not *no localization
  // route is anonymous*.
  //
  // The universal belongs to `PlatformLocalizationRouteInventoryTests`, whose `Expected` static is proved
  // set-equal to the LIVE ROUTE TABLE by a sibling test, so its per-row claim really is about the whole
  // surface. **Set-equality against the running application is what closes a population; set-equality
  // against a generated document is one step short of it.**
  [Fact]
  [Trait("Criterion", "AC-LOC-0062")]
  public async Task All_nine_routes_apply_the_required_security_headers_to_authentication_failures()
  {
    var routes = ApprovedRoutes.Select(route => (route.Method, Path: Concrete(route.Template))).ToArray();
    var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
      services.RemoveAll<IRequestTenantEligibility>();
      services.AddScoped<IRequestTenantEligibility, ActiveTenantEligibility>();
    })).CreateClient();

    foreach (var (method, path) in routes)
    {
      using var request = new HttpRequestMessage(method, path);
      using var response = await client.SendAsync(request);
      using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

      Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
        $"{method} {path} returned {(int)response.StatusCode} instead of 401.");
      Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
      Assert.Equal("no-cache", response.Headers.Pragma.ToString());
      Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
      Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
      Assert.Equal("platform.authentication.errors.authentication_failed",
        problem.RootElement.GetProperty("resourceKey").GetString());
    }
  }

  // ⚠ CITES THE *"exact OpenAPI"* CLAUSE OF `AC-LOC-0042` (list) AND `AC-LOC-0043` (get resource). Both
  // sentences end *"…and exact OpenAPI"*, and this pins the generated document: the localization path set is
  // EXACTLY these nine, each exposes exactly one operation, each carries a `Bearer` security requirement,
  // and none exposes `delete`.
  //
  // ⚠⚠ THIS IS THE ONE ASSERTION IN THE FILE WHOSE POPULATION IS CLOSED AGAINST REALITY. `Assert.Equal(
  // expected.Keys.Order(), localization.Keys.Order())` compares the hand-written set to THE GENERATED
  // DOCUMENT, so a tenth localization route cannot appear without reddening. Every other clause of 0042 and
  // 0043 — auth, `View`, current live Tenant, strict bounded filters, paging, safe raw-template projection,
  // safe not-found, status codes, cross-Tenant denial — is NOT asserted here; the raw-template projection
  // clause is carried by `LocalizationAdministrationTemplateTests` at the handler layer.
  //
  // ⚠⚠⚠ AND *EXACT OPENAPI* IS A CLAUSE ABOUT THE DOCUMENT, WHICH IS THE ONLY REASON A DOCUMENT ASSERTION
  // CAN SATISFY IT. The same shape would be a mistake anywhere else in these sentences: `:139` asserts that
  // the `expectedRowVersion` DESCRIPTION mentions *padded RFC 4648 Base64*, and that is deliberately NOT
  // cited to `AC-LOC-0061` — a description saying what the contract is is not the contract behaving.
  [Fact]
  [Trait("Criterion", "AC-LOC-0042")]
  [Trait("Criterion", "AC-LOC-0043")]
  public async Task Generated_document_exposes_exactly_the_nine_approved_localization_routes_and_authentication()
  {
    using var document = await GetDocumentAsync();
    var paths = document.RootElement.GetProperty("paths");
    var localization = paths.EnumerateObject()
      .Where(path => path.Name.StartsWith("/api/platform/localization", StringComparison.Ordinal))
      .ToDictionary(path => path.Name, path => path.Value, StringComparer.Ordinal);
    var expected = ApprovedRoutes.ToDictionary(
      route => route.Template,
      route => route.Method.Method.ToLowerInvariant(),
      StringComparer.Ordinal);

    Assert.Equal(expected.Keys.Order(), localization.Keys.Order());
    foreach (var (path, method) in expected)
    {
      var operation = localization[path].GetProperty(method);
      Assert.Equal(method, localization[path].EnumerateObject().Single(property => property.Name is not "parameters").Name);
      Assert.Contains(operation.GetProperty("security").EnumerateArray(), requirement =>
        requirement.EnumerateObject().Any(entry => entry.Name == "Bearer"));
      Assert.False(localization[path].TryGetProperty("delete", out _));
    }

    Assert.Contains("No localization administrative permission", localization["/api/platform/localization/effective"]
      .GetProperty("get").GetProperty("description").GetString(), StringComparison.Ordinal);
    Assert.Contains("Platform.Localization.Manage", localization["/api/platform/localization/preview"]
      .GetProperty("post").GetProperty("description").GetString(), StringComparison.Ordinal);
  }

  // ⚠ CITES THE *"and OpenAPI"* CLAUSE OF `AC-LOC-0050` (effective batch). The sentence requires *"strict
  // UNIQUE BOUNDED KEYS / CULTURE / optional resource-scoped PLAIN-STRING placeholder values"*, and the
  // document is pinned to exactly that: `resourceKeys` `maxItems` 100 and `uniqueItems` true, `culture`
  // enumerated to `["en","ar"]`, and `placeholderValuesByResource` an object of `maxProperties` 100 whose
  // nested `additionalProperties` are `string` — which is what *plain-string* means in the schema.
  //
  // ⚠⚠ DECLARED, NOT ENFORCED, AND THE DISTINCTION IS THE WHOLE CITATION. Every number here is read out of
  // the generated document; NONE of it exercises the handler. `LocalizationTextResolver` carries its own
  // `MaximumExplicitBatchSize` and returns `ExplicitBatchTooLarge`, and 0050's remaining clauses — malformed
  // or unrequested maps failing REQUEST validation, missing/unknown placeholders failing POLICY validation,
  // and runtime resolution not requiring `View` — are behaviour and are not touched here. **A schema bound
  // and a runtime bound can disagree, and this test would stay green if they did.**
  //
  // ⚠ Also examined and NOT cited: `AC-LOC-0061` (see the note above — `:139` reads a description string),
  // and the `ProblemDetails` required-property and 400/401/403/409/422/503 assertions, which pin the
  // document's error surface without exercising any of those responses.
  [Fact]
  [Trait("Criterion", "AC-LOC-0050")]
  public async Task Generated_document_locks_request_shapes_culture_limits_rowversions_and_problem_responses()
  {
    using var document = await GetDocumentAsync();
    var root = document.RootElement;
    var paths = root.GetProperty("paths");

    Assert.Equal(["value", "expectedRowVersion"], RequestProperties(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}", "put"));
    Assert.Equal(["targetVersionNumber", "expectedRowVersion"], RequestProperties(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo", "post"));
    Assert.Equal(["expectedRowVersion"], RequestProperties(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default", "post"));
    Assert.Equal(["resourceKey", "culture", "value"], RequestProperties(root, paths, "/api/platform/localization/preview", "post"));
    Assert.Equal(["culture", "resourceKeys", "placeholderValuesByResource"],
      RequestProperties(root, paths, "/api/platform/localization/effective/batch", "post"));

    foreach (var (path, method) in new[]
    {
      ("/api/platform/localization/resources/{resourceKey}/overrides/{culture}", "put"),
      ("/api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo", "post"),
      ("/api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default", "post"),
      ("/api/platform/localization/preview", "post"),
      ("/api/platform/localization/effective/batch", "post")
    })
    {
      var schema = RequestSchema(root, paths, path, method);
      Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
      var required = schema.GetProperty("required").EnumerateArray().Select(property => property.GetString()).Order().ToArray();
      if (path == "/api/platform/localization/effective/batch")
      {
        Assert.Equal(["culture", "resourceKeys"], required);
      }
      else
      {
        Assert.Equal(schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(), required);
      }
    }

    var batchSchema = RequestSchema(root, paths, "/api/platform/localization/effective/batch", "post");
    Assert.Equal(100, batchSchema.GetProperty("properties").GetProperty("resourceKeys").GetProperty("maxItems").GetInt32());
    Assert.True(batchSchema.GetProperty("properties").GetProperty("resourceKeys").GetProperty("uniqueItems").GetBoolean());
    Assert.Equal(["en", "ar"], batchSchema.GetProperty("properties").GetProperty("culture").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    var placeholderValues = batchSchema.GetProperty("properties").GetProperty("placeholderValuesByResource");
    Assert.Equal("object", placeholderValues.GetProperty("type").GetString());
    Assert.Equal(100, placeholderValues.GetProperty("maxProperties").GetInt32());
    Assert.Equal("string", placeholderValues.GetProperty("additionalProperties").GetProperty("additionalProperties")
      .GetProperty("type").GetString());

    var putSchema = RequestSchema(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}", "put");
    var rowVersion = putSchema.GetProperty("properties").GetProperty("expectedRowVersion");
    Assert.Equal("string", rowVersion.GetProperty("type").GetString());
    Assert.Contains("padded RFC 4648 Base64", rowVersion.GetProperty("description").GetString(), StringComparison.Ordinal);

    var effective = paths.GetProperty("/api/platform/localization/effective").GetProperty("get");
    Assert.Equal(["en", "ar"], effective.GetProperty("parameters").EnumerateArray()
      .Single(parameter => parameter.GetProperty("name").GetString() == "culture")
      .GetProperty("schema").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    Assert.Contains("250", effective.GetProperty("parameters").EnumerateArray()
      .Single(parameter => parameter.GetProperty("name").GetString() == "group").GetProperty("description").GetString(), StringComparison.Ordinal);
    Assert.Equal(100, paths.GetProperty("/api/platform/localization/resources").GetProperty("get").GetProperty("parameters")
      .EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == "pageSize").GetProperty("schema").GetProperty("maximum").GetInt32());
    Assert.Equal(
      ["category", "culture", "group", "incompatibleOnly", "lifecycle", "module", "overriddenOnly", "pageNumber", "pageSize", "search", "securityClassification"],
      QueryParameterNames(paths, "/api/platform/localization/resources", "get"));
    Assert.Equal(["culture"], QueryParameterNames(paths, "/api/platform/localization/resources/{resourceKey}", "get"));
    Assert.Equal(["culture", "pageNumber", "pageSize"],
      QueryParameterNames(paths, "/api/platform/localization/resources/{resourceKey}/history", "get"));

    foreach (var status in new[] { "400", "401", "403", "409", "422", "503" })
    {
      Assert.True(paths.GetProperty("/api/platform/localization/resources/{resourceKey}/overrides/{culture}")
        .GetProperty("put").GetProperty("responses").TryGetProperty(status, out var problem));
      Assert.Equal("#/components/schemas/ProblemDetails", problem.GetProperty("content").GetProperty("application/problem+json")
        .GetProperty("schema").GetProperty("$ref").GetString());
    }
    Assert.False(effective.GetProperty("responses").TryGetProperty("503", out _));
    Assert.False(effective.GetProperty("responses").TryGetProperty("422", out _));
    Assert.Contains("raw effective localization templates", effective.GetProperty("description").GetString(), StringComparison.Ordinal);
    Assert.True(paths.GetProperty("/api/platform/localization/effective/batch").GetProperty("post")
      .GetProperty("responses").TryGetProperty("422", out _));

    var put = paths.GetProperty("/api/platform/localization/resources/{resourceKey}/overrides/{culture}").GetProperty("put");
    Assert.Equal("#/components/schemas/LocalizationMutationResponse", ResponseSchemaReference(put, "200"));
    Assert.Equal("#/components/schemas/LocalizationMutationResponse", ResponseSchemaReference(put, "201"));

    var problemSchema = root.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");
    Assert.Equal(["code", "correlationId", "resourceKey", "status", "type"], problemSchema.GetProperty("required")
      .EnumerateArray().Select(item => item.GetString()).Order());
  }

  private async Task<JsonDocument> GetDocumentAsync()
  {
    var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
    response.EnsureSuccessStatusCode();
    return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
  }

  private static string[] RequestProperties(JsonElement root, JsonElement paths, string path, string method) =>
    RequestSchema(root, paths, path, method).GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();

  private static JsonElement RequestSchema(JsonElement root, JsonElement paths, string path, string method)
  {
    var schema = paths.GetProperty(path).GetProperty(method).GetProperty("requestBody")
      .GetProperty("content").GetProperty("application/json").GetProperty("schema");
    return schema.TryGetProperty("$ref", out var reference)
      ? root.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/').Last())
      : schema;
  }

  private static string?[] QueryParameterNames(JsonElement paths, string path, string method) =>
    paths.GetProperty(path).GetProperty(method).GetProperty("parameters").EnumerateArray()
      .Where(parameter => parameter.GetProperty("in").GetString() == "query")
      .Select(parameter => parameter.GetProperty("name").GetString()).Order().ToArray();

  private static string? ResponseSchemaReference(JsonElement operation, string status) =>
    operation.GetProperty("responses").GetProperty(status).GetProperty("content").GetProperty("application/json")
      .GetProperty("schema").GetProperty("$ref").GetString();

  private sealed class ActiveTenantEligibility : IRequestTenantEligibility
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
  }
}
