using Microsoft.Extensions.DependencyInjection;

namespace SSAS.Attendance.API;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // The endpoint filter that establishes company context for every attendance route. Registered here
    // rather than in AddAttendanceInfrastructure because it is transport, not persistence -- the same split
    // HR, GL and Payroll use.
    services.AddScoped<AttendanceCompanyContextEndpointFilter>();

    return services;
  }
}
