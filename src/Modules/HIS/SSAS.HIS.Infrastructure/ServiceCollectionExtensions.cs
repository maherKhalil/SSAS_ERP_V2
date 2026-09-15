using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.HIS.Infrastructure.Persistence;

namespace SSAS.HIS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<HisDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("HisDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new System.InvalidOperationException(
                    "ConnectionStrings:HisDatabase is required to use HIS persistence.");
            }

            options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
                "__HisMigrationsHistory",
                "his"));
        });
        
        services.AddScoped<IHisDbContext>(sp => sp.GetRequiredService<HisDbContext>());
        services.AddScoped<SSAS.HIS.Application.Registration.Patients.IPatientRepository, PatientRepository>();
        services.AddScoped<SSAS.HIS.Application.IHisReadService, HisReadService>();

        return services;
    }
}
