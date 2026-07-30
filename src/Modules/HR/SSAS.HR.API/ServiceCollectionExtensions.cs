using Microsoft.Extensions.DependencyInjection;

namespace SSAS.HR.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddHrModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    return services;
  }
}
