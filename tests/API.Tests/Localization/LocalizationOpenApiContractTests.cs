using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAS.API.Tests.Infrastructure;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.API.Tests.Localization;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class LocalizationOpenApiContractTests(HostWebApplicationFactory factory)
{
  // ⚠ CITES `AC-LOC-0062`'s FIRST CLAUSE — *"All nine M2 routes are NON-ANONYMOUS"* — AT THE OTHER LAYER.
  // `PlatformLocalizationRouteInventoryTests` already cites this clause and asserts `HasAuthorization`, i.e.
  // that the METADATA IS DECLARED. This asserts the OBSERVABLE: an anonymous request to each of the nine
  // returns 401, with the no-store/no-cache/no-referrer/nosniff headers and the failure resource key.
  // **Declared and enforced are different claims and this is the second one**; neither test subsumes the
  // other, and a route could carry `[Authorize]` metadata that some later middleware never honours.
  //
  // ⚠⚠ RESIDUAL, AND IT IS THE REASON THIS DOES NOT ALSO CARRY `AC-LOC-0053` ("Milestone 2 exposes no
  // anonymous localization HTTP route"): THE POPULATION HERE IS A HAND-WRITTEN LIST, NOT A PINNED SET. The
  // inventory test loops over a SHARED `Expected` static that a sibling test proves set-equal to the live
  // route table, so its per-row claim really is a claim about the whole surface. The nine paths below and
  // the nine in `Generated_document_exposes_…` are SEPARATE LITERALS in different spellings (concrete keys
  // here, `{resourceKey}` templates there). **A tenth route would redden the document test, and once its
  // list was updated nothing would notice that this one still had nine** — so the universal belongs to the
  // inventory test and the observation belongs here. See the population rule: a control must share the
  // instrument, and this one does not.
  [Fact]
  [Trait("Criterion", "AC-LOC-0062")]
  public async Task All_nine_routes_apply_the_required_security_headers_to_authentication_failures()
  {
    var routes = new (HttpMethod Method, string Path)[]
    {
      (HttpMethod.Get, "/api/platform/localization/resources"),
      (HttpMethod.Get, "/api/platform/localization/resources/platform.common.actions.save"),
      (HttpMethod.Put, "/api/platform/localization/resources/platform.common.actions.save/overrides/en"),
      (HttpMethod.Post, "/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo"),
      (HttpMethod.Post, "/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default"),
      (HttpMethod.Get, "/api/platform/localization/resources/platform.common.actions.save/history"),
      (HttpMethod.Post, "/api/platform/localization/preview"),
      (HttpMethod.Get, "/api/platform/localization/effective"),
      (HttpMethod.Post, "/api/platform/localization/effective/batch")
    };
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
    var expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["/api/platform/localization/resources"] = "get",
      ["/api/platform/localization/resources/{resourceKey}"] = "get",
      ["/api/platform/localization/resources/{resourceKey}/overrides/{culture}"] = "put",
      ["/api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo"] = "post",
      ["/api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default"] = "post",
      ["/api/platform/localization/resources/{resourceKey}/history"] = "get",
      ["/api/platform/localization/preview"] = "post",
      ["/api/platform/localization/effective"] = "get",
      ["/api/platform/localization/effective/batch"] = "post"
    };

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
