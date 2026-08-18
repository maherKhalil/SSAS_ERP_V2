using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Companies;

namespace SSAS.Platform.Infrastructure.RequestContext;

public static class RequestContextServiceCollectionExtensions
{
  public static IServiceCollection AddPlatformRequestContext(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddHttpContextAccessor();
    services.AddScoped<CurrentUser>();
    services.AddScoped<ICurrentUser>(serviceProvider => serviceProvider.GetRequiredService<CurrentUser>());
    services.AddScoped<CurrentTenant>();
    services.AddScoped<ICurrentTenant>(serviceProvider => serviceProvider.GetRequiredService<CurrentTenant>());

    // ---- FP-006C1: THE COMPANY REQUEST CONTEXT (ADR-025 decisions 2 and 4).
    //
    // TWO REGISTRATIONS, TWO DIFFERENT THINGS, and keeping them apart is the point. ICompanySelection is
    // caller INTENT read from the `X-Company-Id` header and carries no authority whatsoever. ICurrentCompany
    // is the TRUSTED value, and is empty until CurrentCompany.EstablishAsync has run the five-step live
    // validation against the tenant database and the access resolver.
    //
    // Neither is registered as an authorization source for the write boundary: saves go through
    // ICompanyWriteAuthorizer, which re-asks the validation, because a value established at the start of a
    // request is exactly what must not be trusted at save time.
    services.AddScoped<ICompanySelection, RequestedCompanySelection>();
    services.AddScoped<CurrentCompany>();
    services.AddScoped<ICurrentCompany>(serviceProvider => serviceProvider.GetRequiredService<CurrentCompany>());
    services.AddScoped<ICurrentAuthenticationSession, CurrentAuthenticationSessionAccessor>();
    services.AddScoped<CorrelationContext>();
    services.AddScoped<ICorrelationContext>(serviceProvider => serviceProvider.GetRequiredService<CorrelationContext>());
    services.AddScoped<IRequestMetadata, RequestMetadata>();
    services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
    return services;
  }
}
