using System.Net;
using SSAS.Host.API.Diagnostics;
using SSAS.API.Tests.Infrastructure;

namespace SSAS.API.Tests.Infrastructure;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class HostEndpointTests(HostWebApplicationFactory factory)
{
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
}
