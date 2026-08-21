using Microsoft.Extensions.DependencyInjection;
using SSAS.HR.API.Employees;

namespace SSAS.HR.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddHrModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // The company-context filter is resolved per request from the container, so it participates in the
    // Host's service-provider validation like any other dependency rather than being newed up at map time.
    services.AddScoped<CompanyContextEndpointFilter>();

    // The two deferred wire fields are composed per request from an employee scope and a Platform-owned
    // company fact, so their composer is scoped like everything else it depends on (FP-008 Phase 4).
    services.AddScoped<Positions.PositionCompositionServices>();

    return services;
  }
}
