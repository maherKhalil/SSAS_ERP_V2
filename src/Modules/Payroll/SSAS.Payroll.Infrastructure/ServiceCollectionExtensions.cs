using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Compensation;
using SSAS.Payroll.Application.Elements;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Application.Runs;
using SSAS.Payroll.Infrastructure.Persistence;

namespace SSAS.Payroll.Infrastructure;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddPayrollInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // ---- PAYROLL'S CONTRIBUTION TO THE SINGLE TENANT MODEL.
    //
    // Registered EXPLICITLY, never discovered. Without this line Payroll's six entities are absent from the
    // tenant model, absent from the migration stream, and -- because TenantCutoverCopyPlan derives its
    // manifest from the model -- absent from Shared-to-Dedicated cutover, which fails SILENTLY.
    services.AddSingleton<ITenantModelContributor, PayrollTenantModelContributor>();

    services.AddScoped<IPayElementRepository, PayElementRepository>();
    services.AddScoped<IEmployeeCompensationRepository, EmployeeCompensationRepository>();
    services.AddScoped<IOneOffPaymentRepository, OneOffPaymentRepository>();
    services.AddScoped<IPayrollPeriodRepository, PayrollPeriodRepository>();
    services.AddScoped<IPayrollRunRepository, PayrollRunRepository>();

    services.AddScoped<IPayrollScopeResolver, PayrollScopeResolver>();

    // FP-015's self-service scope (T-088). A SEPARATE registration, not a widening of the one above: every
    // Payroll command handler takes IPayrollScopeResolver, so adding self-service dependencies to it made
    // them construction-time dependencies of every payroll write — twenty-five API tests failed DI
    // validation before this was split out.
    services.AddScoped<IPayrollSelfServiceScopeResolver, PayrollSelfServiceScopeResolver>();
    services.AddScoped<IPayrollReadService, PayrollReadService>();

    services.AddScoped<CreatePayElementCommandHandler>();
    services.AddScoped<UpdatePayElementCommandHandler>();
    services.AddScoped<SetPayElementActivationCommandHandler>();

    services.AddScoped<RecordCompensationCommandHandler>();

    services.AddScoped<GeneratePayrollPeriodCommandHandler>();
    services.AddScoped<CreatePayrollRunCommandHandler>();
    services.AddScoped<CalculatePayrollRunCommandHandler>();
    services.AddScoped<ApprovePayrollRunCommandHandler>();
    services.AddScoped<PostPayrollRunCommandHandler>();
    services.AddScoped<ReversePayrollRunCommandHandler>();

    return services;
  }
}
