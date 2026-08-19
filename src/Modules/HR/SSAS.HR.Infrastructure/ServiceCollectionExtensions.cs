using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Infrastructure.Persistence;

namespace SSAS.HR.Infrastructure;

// HR infrastructure registration (ADR-012).
//
// Called by the Host, which is the one place permitted to see a module's Infrastructure. HR registers its
// own repositories and its own contribution to the tenant model; it registers nothing of Platform's, and
// Platform registers nothing of HR's.
public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddHrInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // ---- HR'S ENTITIES ENTER THE ONE TENANT MODEL (ADR-017, ADR-012).
    //
    // SINGLETON, and deliberately: the contributor is stateless and deterministic, and its TYPE is what
    // participates in the EF model cache key. Registering it per-scope would neither change the model nor
    // the key, but it would suggest the mapping could vary — which is exactly what must never be true.
    services.AddSingleton<ITenantModelContributor, HrTenantModelContributor>();

    services.AddScoped<IEmployeeRepository, EmployeeRepository>();

    // ---- THE READ SIDE (FP-006C4).
    //
    // The resolver is SCOPED because everything it consults — the acting user, the selected company, the
    // execution branch — is per-request, and because a longer lifetime would be a cache of authorization
    // state, which is precisely what ADR-025 decision 7 forbids.
    services.AddScoped<IEmployeeScopeResolver, EmployeeScopeResolver>();
    services.AddScoped<IEmployeeReadService, EmployeeReadService>();

    services.AddScoped<CreateEmployeeCommandHandler>();
    services.AddScoped<UpdateEmployeeProfileCommandHandler>();
    services.AddScoped<TerminateEmployeeCommandHandler>();
    services.AddScoped<TransferEmployeeCommandHandler>();

    services.AddScoped<GetEmployeeQueryHandler>();
    services.AddScoped<SearchEmployeesQueryHandler>();
    services.AddScoped<GetEmployeeBranchHistoryQueryHandler>();

    return services;
  }
}
