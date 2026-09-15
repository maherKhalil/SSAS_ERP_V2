using Microsoft.Extensions.DependencyInjection;
using SSAS.HIS.Application.Registration.Patients.Commands.RegisterPatient;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;
using SSAS.HIS.Application.Registration.Patients.Queries.SearchPatients;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetDoctorsLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetSpecialtiesLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetClinicsLookup;

namespace SSAS.HIS.API;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHisModule(this IServiceCollection services)
    {
        services.AddScoped<RegisterPatientCommandHandler>();
        services.AddScoped<GetPatientByIdQueryHandler>();
        services.AddScoped<SearchPatientsQueryHandler>();

        services.AddScoped<GetDoctorsLookupQueryHandler>();
        services.AddScoped<GetSpecialtiesLookupQueryHandler>();
        services.AddScoped<GetClinicsLookupQueryHandler>();

        return services;
    }
}
