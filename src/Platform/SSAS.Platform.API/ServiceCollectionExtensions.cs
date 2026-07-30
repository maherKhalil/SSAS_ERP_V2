using Microsoft.Extensions.DependencyInjection;

namespace SSAS.Platform.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddPlatformModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    return services;
  }
}
