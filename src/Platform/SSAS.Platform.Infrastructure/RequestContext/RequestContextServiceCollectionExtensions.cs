using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Authentication;

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
    services.AddScoped<ICurrentAuthenticationSession, CurrentAuthenticationSessionAccessor>();
    services.AddScoped<CorrelationContext>();
    services.AddScoped<ICorrelationContext>(serviceProvider => serviceProvider.GetRequiredService<CorrelationContext>());
    services.AddScoped<IRequestMetadata, RequestMetadata>();
    services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
    return services;
  }
}
