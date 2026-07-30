using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace SSAS.Host.API.Configuration;

public static class HostEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapHostEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    endpoints.MapGet("/", (IOptions<ApplicationOptions> options, IHostEnvironment environment) => Results.Ok(new
      {
        application = options.Value.Name,
        version = options.Value.Version,
        environment = environment.EnvironmentName
      }))
      .WithName("ApplicationInformation")
      .AllowAnonymous();

    endpoints.MapHealthChecks("/health", new HealthCheckOptions())
      .AllowAnonymous();
    endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
      {
        Predicate = registration => registration.Tags.Contains("live")
      })
      .AllowAnonymous();
    endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
      {
        Predicate = registration => registration.Tags.Contains("ready")
      })
      .AllowAnonymous();

    return endpoints;
  }
}
