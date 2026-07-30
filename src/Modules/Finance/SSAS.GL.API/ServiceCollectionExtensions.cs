using Microsoft.Extensions.DependencyInjection;

namespace SSAS.GL.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGlModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    return services;
  }
}
