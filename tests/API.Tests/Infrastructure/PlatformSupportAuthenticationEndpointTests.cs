using System.Net;
using System.Text;
using System.Text.Json;
using SSAS.Host.API.Diagnostics;

namespace SSAS.API.Tests.Infrastructure;

// Phase 4B (DEC-TEN-0023) platform-support authentication HTTP surface — OpenApi shape + anonymous reject paths
// through the REAL host pipeline. The authenticated logout plane guard is proven end-to-end with signed JWTs in
// PlatformSupportAuthenticationLogoutPipelineTests.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class PlatformSupportAuthenticationEndpointTests(HostWebApplicationFactory factory)
{
  private const string Prefix = "/api/platform/support/auth";

  [Fact]
  public async Task OpenApi_exposes_the_platform_support_auth_routes_with_the_expected_security_shape()
  {
    var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
    response.EnsureSuccessStatusCode();
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var paths = document.RootElement.GetProperty("paths");

    foreach (var route in new[] { "login", "refresh", "logout" })
    {
      Assert.True(paths.TryGetProperty($"{Prefix}/{route}", out var path), $"{route} route is missing.");
      var operation = path.GetProperty("post");
      var responses = operation.GetProperty("responses");
      foreach (var status in new[] { "400", "401", "403", "429", "503" })
        Assert.True(responses.TryGetProperty(status, out _), $"{route} is missing {status}.");
    }

    // login + refresh are anonymous (no Bearer security requirement); logout requires Bearer and returns 204.
    var login = paths.GetProperty($"{Prefix}/login").GetProperty("post");
    Assert.False(login.TryGetProperty("security", out var loginSecurity) && loginSecurity.GetArrayLength() > 0);
    var refresh = paths.GetProperty($"{Prefix}/refresh").GetProperty("post");
    Assert.False(refresh.TryGetProperty("security", out var refreshSecurity) && refreshSecurity.GetArrayLength() > 0);
    Assert.False(refresh.TryGetProperty("requestBody", out _));
    // refresh + logout document the double-submit CSRF header requirement.
    Assert.Contains(refresh.GetProperty("parameters").EnumerateArray(), parameter =>
      parameter.GetProperty("name").GetString() == "X-XSRF-TOKEN" && parameter.GetProperty("required").GetBoolean());
    var logout = paths.GetProperty($"{Prefix}/logout").GetProperty("post");
    Assert.Equal("Bearer", logout.GetProperty("security")[0].EnumerateObject().Single().Name);
    Assert.True(logout.GetProperty("responses").TryGetProperty("204", out _));
    Assert.Contains(logout.GetProperty("parameters").EnumerateArray(), parameter =>
      parameter.GetProperty("name").GetString() == "X-XSRF-TOKEN" && parameter.GetProperty("required").GetBoolean());
  }

  [Fact]
  public async Task Login_rejects_http_instead_of_redirecting()
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost{Prefix}/login")
    {
      Content = new StringContent("{\"loginEmail\":\"operator@example.test\",\"password\":\"secret\"}", Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Origin", "https://localhost:4200");

    var response = await factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    await AssertProblemCodeAsync(response, "authentication.request_rejected");
  }

  [Fact]
  public async Task Login_rejects_unknown_input_fields_without_echoing_the_body()
  {
    const string body = "{\"loginEmail\":\"operator@example.test\",\"password\":\"do-not-echo\",\"clientId\":\"caller-value\"}";
    using var request = new HttpRequestMessage(HttpMethod.Post, $"https://localhost{Prefix}/login")
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
    using var request = new HttpRequestMessage(HttpMethod.Post, $"https://localhost{Prefix}/refresh");
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
