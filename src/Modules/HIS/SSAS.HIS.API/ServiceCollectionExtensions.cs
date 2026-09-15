using Microsoft.Extensions.DependencyInjection;
using SSAS.HIS.Application.Registration.Patients.Commands.RegisterPatient;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;
using SSAS.HIS.Application.Registration.Patients.Queries.SearchPatients;

namespace SSAS.HIS.API;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHisModule(this IServiceCollection services)
    {
        services.AddScoped<RegisterPatientCommandHandler>();
        services.AddScoped<GetPatientByIdQueryHandler>();
        services.AddScoped<SearchPatientsQueryHandler>();

        return services;
    }
}
