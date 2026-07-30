using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Infrastructure.Identity;

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
    services.AddScoped<CorrelationContext>();
    services.AddScoped<ICorrelationContext>(serviceProvider => serviceProvider.GetRequiredService<CorrelationContext>());
    services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
    services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<object>, Microsoft.AspNetCore.Identity.PasswordHasher<object>>();
    services.AddSingleton<IPasswordHashingService, AspNetPasswordHashingService>();

    return services;
  }
}
