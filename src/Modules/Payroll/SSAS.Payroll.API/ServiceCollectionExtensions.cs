using Microsoft.Extensions.DependencyInjection;

namespace SSAS.Payroll.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddPayrollModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // The endpoint filter that establishes company context for every payroll route. Registered here rather
    // than in AddPayrollInfrastructure because it is transport, not persistence -- the same split HR and GL
    // use.
    services.AddScoped<PayrollCompanyContextEndpointFilter>();

    return services;
  }
}
