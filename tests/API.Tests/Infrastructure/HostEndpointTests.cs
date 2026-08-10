using System.Net;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SSAS.Host.API.Diagnostics;
using SSAS.API.Tests.Infrastructure;
using SSAS.Host.API.Authorization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;

namespace SSAS.API.Tests.Infrastructure;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class HostEndpointTests(HostWebApplicationFactory factory)
{
  [Fact]
  public void Development_host_uses_an_ephemeral_data_protection_provider()
  {
    var provider = factory.Services.GetRequiredService<IDataProtectionProvider>();

    Assert.Contains("Ephemeral", provider.GetType().Name, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Root_propagates_a_valid_correlation_id_to_the_response()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/");
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "request-correlation-123");

    var response = await factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("request-correlation-123", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
  }

  [Fact]
  public async Task Root_replaces_an_invalid_correlation_id_with_a_generated_value()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, "/");
    request.Headers.Add(CorrelationIdMiddleware.HeaderName, "invalid/correlation/id");

    var response = await factory.CreateClient().SendAsync(request);
    var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotEqual("invalid/correlation/id", correlationId);
    Assert.True(Guid.TryParseExact(correlationId, "N", out _));
  }

  [Theory]
  [InlineData("/health/live")]
  [InlineData("/health/ready")]
  public async Task Operational_health_endpoints_return_healthy(string path)
  {
    var response = await factory.CreateClient().GetAsync(path);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public void Readiness_health_and_shared_tenant_eligibility_are_composed()
  {
    var registrations = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
    var localization = Assert.Single(registrations, registration =>
      registration.Name == "localization_management_audit_readiness");
    Assert.Contains("ready", localization.Tags);

    Assert.Contains(typeof(LiveTenantEligibilityAuthorization).GetConstructors().Single().GetParameters(),
      parameter => parameter.ParameterType == typeof(IRequestTenantEligibility));
    Assert.Contains(typeof(LocalizationTextResolver).GetConstructors().Single().GetParameters(),
      parameter => parameter.ParameterType == typeof(IRequestTenantEligibility));
  }

  [Fact]
  public async Task OpenApi_exposes_only_the_approved_platform_authentication_routes()
  {
    var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
    response.EnsureSuccessStatusCode();
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var paths = document.RootElement.GetProperty("paths");

    Assert.True(paths.TryGetProperty("/api/platform/auth/login", out _));
    Assert.True(paths.TryGetProperty("/api/platform/auth/select-tenant", out _));
    Assert.True(paths.TryGetProperty("/api/platform/auth/refresh", out _));
    Assert.True(paths.TryGetProperty("/api/platform/auth/logout", out _));
    Assert.DoesNotContain(paths.EnumerateObject(), path => path.Name.StartsWith("/api/auth/", StringComparison.Ordinal));

    var bearer = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
    Assert.Equal("http", bearer.GetProperty("type").GetString());
    Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    foreach (var route in new[] { "login", "select-tenant", "refresh", "logout" })
    {
      var operation = paths.GetProperty($"/api/platform/auth/{route}").GetProperty("post");
      var responses = operation.GetProperty("responses");
      foreach (var status in new[] { "400", "401", "403", "429", "503" })
        Assert.True(responses.TryGetProperty(status, out _), $"{route} is missing {status}.");
    }

    var login = paths.GetProperty("/api/platform/auth/login").GetProperty("post");
    Assert.Equal(2, login.GetProperty("responses").GetProperty("200").GetProperty("content")
      .GetProperty("application/json").GetProperty("schema").GetProperty("oneOf").GetArrayLength());
    var refresh = paths.GetProperty("/api/platform/auth/refresh").GetProperty("post");
    Assert.False(refresh.TryGetProperty("requestBody", out _));
    Assert.Contains(refresh.GetProperty("parameters").EnumerateArray(), parameter =>
      parameter.GetProperty("name").GetString() == "X-XSRF-TOKEN" && parameter.GetProperty("required").GetBoolean());
    var logout = paths.GetProperty("/api/platform/auth/logout").GetProperty("post");
    Assert.Contains(logout.GetProperty("parameters").EnumerateArray(), parameter =>
      parameter.GetProperty("name").GetString() == "X-XSRF-TOKEN" && parameter.GetProperty("required").GetBoolean());
    Assert.Equal("Bearer", logout.GetProperty("security")[0].EnumerateObject().Single().Name);
    Assert.False(login.TryGetProperty("security", out var loginSecurity) && loginSecurity.GetArrayLength() > 0);
  }

  [Fact]
  public async Task OpenApi_documents_the_roles_admin_route_with_shared_conventions()
  {
    var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
    response.EnsureSuccessStatusCode();
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var paths = document.RootElement.GetProperty("paths");

    Assert.True(paths.TryGetProperty("/api/platform/roles", out var roles));
    var get = roles.GetProperty("get");
    Assert.Equal("Bearer", get.GetProperty("security")[0].EnumerateObject().Single().Name);
    var responses = get.GetProperty("responses");
    foreach (var status in new[] { "200", "400", "401", "403" })
    {
      Assert.True(responses.TryGetProperty(status, out _), $"roles is missing {status}.");
    }

    var parameterNames = get.GetProperty("parameters").EnumerateArray()
      .Select(parameter => parameter.GetProperty("name").GetString()).ToArray();
    Assert.Contains("pageNumber", parameterNames);
    Assert.Contains("pageSize", parameterNames);
  }

  [Fact]
  public async Task Authentication_login_rejects_http_instead_of_redirecting()
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/platform/auth/login")
    {
      Content = new StringContent("{\"loginEmail\":\"user@example.test\",\"password\":\"secret\"}", Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Origin", "https://localhost:4200");

    var response = await factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await AssertProblemCodeAsync(response, "authentication.request_rejected");
  }

  [Fact]
  public async Task Authentication_login_rejects_unknown_input_fields_without_echoing_the_body()
  {
    const string body = "{\"loginEmail\":\"user@example.test\",\"password\":\"do-not-echo\",\"clientId\":\"caller-value\"}";
    using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/platform/auth/login")
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Origin", "https://localhost:4200");

    var response = await factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var responseBody = await response.Content.ReadAsStringAsync();
    Assert.Contains("request.invalid", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("do-not-echo", responseBody, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Refresh_rejects_missing_csrf_before_application_dispatch()
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/platform/auth/refresh");
    request.Headers.Add("Origin", "https://localhost:4200");

    var response = await factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await AssertProblemCodeAsync(response, "authentication.request_rejected");
  }

  private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
  }

}
