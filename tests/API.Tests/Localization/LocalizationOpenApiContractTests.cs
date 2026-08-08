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
  [Fact]
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

  [Fact]
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

  [Fact]
  public async Task Generated_document_locks_request_shapes_culture_limits_rowversions_and_problem_responses()
  {
    using var document = await GetDocumentAsync();
    var root = document.RootElement;
    var paths = root.GetProperty("paths");

    Assert.Equal(["value", "expectedRowVersion"], RequestProperties(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}", "put"));
    Assert.Equal(["targetVersionNumber", "expectedRowVersion"], RequestProperties(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo", "post"));
    Assert.Equal(["expectedRowVersion"], RequestProperties(root, paths, "/api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default", "post"));
    Assert.Equal(["resourceKey", "culture", "value"], RequestProperties(root, paths, "/api/platform/localization/preview", "post"));
    Assert.Equal(["culture", "resourceKeys"], RequestProperties(root, paths, "/api/platform/localization/effective/batch", "post"));

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
      Assert.Equal(schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(),
        schema.GetProperty("required").EnumerateArray().Select(property => property.GetString()).Order());
    }

    var batchSchema = RequestSchema(root, paths, "/api/platform/localization/effective/batch", "post");
    Assert.Equal(100, batchSchema.GetProperty("properties").GetProperty("resourceKeys").GetProperty("maxItems").GetInt32());
    Assert.True(batchSchema.GetProperty("properties").GetProperty("resourceKeys").GetProperty("uniqueItems").GetBoolean());
    Assert.Equal(["en", "ar"], batchSchema.GetProperty("properties").GetProperty("culture").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));

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

  private sealed class ActiveTenantEligibility : IRequestTenantEligibility
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
  }
}
