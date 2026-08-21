using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;
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

    services.AddScoped<IDepartmentRepository, DepartmentRepository>();

    // ---- FP-007 PHASE 2. THE DEPARTMENT APPLICATION SURFACE.
    //
    // SCOPED, for the same reason the employee resolver is: everything the resolver consults — the acting
    // user, the selected company — is per-request, and a longer lifetime would be a cache of authorization
    // state, which ADR-025 decision 7 forbids.
    services.AddScoped<IDepartmentScopeResolver, DepartmentScopeResolver>();
    services.AddScoped<IDepartmentReadService, DepartmentReadService>();

    // The hierarchy lock is scoped because it takes the lock on the REQUEST'S tenant connection and enlists
    // in the transaction open on it. A singleton would have no connection to speak of.
    services.AddScoped<IDepartmentHierarchyLock, SqlServerDepartmentHierarchyLock>();

    services.AddScoped<CreateDepartmentCommandHandler>();
    services.AddScoped<UpdateDepartmentCommandHandler>();
    services.AddScoped<ChangeDepartmentParentCommandHandler>();
    services.AddScoped<MoveDepartmentToRootCommandHandler>();
    services.AddScoped<DeactivateDepartmentCommandHandler>();
    services.AddScoped<ReactivateDepartmentCommandHandler>();
    services.AddScoped<AssignDepartmentManagerCommandHandler>();
    services.AddScoped<ClearDepartmentManagerCommandHandler>();

    services.AddScoped<GetDepartmentQueryHandler>();
    services.AddScoped<SearchDepartmentsQueryHandler>();
    services.AddScoped<GetDepartmentChildrenQueryHandler>();

    // ---- FP-008 PHASE 2. THE POSITION APPLICATION SURFACE.
    //
    // THREE AGGREGATES, THREE REPOSITORIES (`OD-POS-002`). All SCOPED, like every other repository here:
    // each resolves the request's tenant context through `ITenantDbContextAccessor`, and a longer lifetime
    // would outlive the connection it reads through.
    services.AddScoped<IPositionRepository, PositionRepository>();
    services.AddScoped<IJobGradeRepository, JobGradeRepository>();
    services.AddScoped<ISalaryGradeRepository, SalaryGradeRepository>();

    // ONE RESOLVER FOR THE THREE FAMILIES, and it is SCOPED for the same reason the department and employee
    // resolvers are: everything it consults — the acting user, the selected company — is per-request, and a
    // longer lifetime would be a cache of authorization state, which ADR-025 decision 7 forbids.
    services.AddScoped<IPositionScopeResolver, PositionScopeResolver>();

    // THREE READ SERVICES, because there are three scope types and each accepts only its own. Registering
    // one over all three would be the first step back toward a shared scope, and `HR.SalaryGrades.View`
    // would stop meaning anything.
    services.AddScoped<IPositionReadService, PositionReadService>();
    services.AddScoped<IJobGradeReadService, JobGradeReadService>();
    services.AddScoped<ISalaryGradeReadService, SalaryGradeReadService>();

    services.AddScoped<CreatePositionCommandHandler>();
    services.AddScoped<UpdatePositionCommandHandler>();
    services.AddScoped<DeactivatePositionCommandHandler>();
    services.AddScoped<ReactivatePositionCommandHandler>();

    services.AddScoped<CreateJobGradeCommandHandler>();
    services.AddScoped<UpdateJobGradeCommandHandler>();
    services.AddScoped<DeactivateJobGradeCommandHandler>();
    services.AddScoped<ReactivateJobGradeCommandHandler>();

    services.AddScoped<CreateSalaryGradeCommandHandler>();
    services.AddScoped<UpdateSalaryGradeCommandHandler>();
    services.AddScoped<DeactivateSalaryGradeCommandHandler>();
    services.AddScoped<ReactivateSalaryGradeCommandHandler>();

    services.AddScoped<GetPositionQueryHandler>();
    services.AddScoped<SearchPositionsQueryHandler>();
    services.AddScoped<GetJobGradeQueryHandler>();
    services.AddScoped<SearchJobGradesQueryHandler>();
    services.AddScoped<GetSalaryGradeQueryHandler>();
    services.AddScoped<SearchSalaryGradesQueryHandler>();

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
    services.AddScoped<ChangeEmployeeDepartmentCommandHandler>();
    services.AddScoped<ChangeEmployeePositionCommandHandler>();

    services.AddScoped<GetEmployeeQueryHandler>();
    services.AddScoped<SearchEmployeesQueryHandler>();
    services.AddScoped<GetEmployeeBranchHistoryQueryHandler>();
    services.AddScoped<GetEmployeePositionHistoryQueryHandler>();

    services.AddScoped<ActivateEmployeeCommandHandler>();
    services.AddScoped<DeactivateEmployeeCommandHandler>();

    return services;
  }
}
