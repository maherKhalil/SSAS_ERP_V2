using Microsoft.Extensions.DependencyInjection;

namespace SSAS.GL.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGlModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // The endpoint filter that establishes company context for every GL route. Registered here rather than
    // in AddGlInfrastructure because it is transport, not persistence -- the same split HR uses.
    services.AddScoped<GlCompanyContextEndpointFilter>();

    return services;
  }
}
