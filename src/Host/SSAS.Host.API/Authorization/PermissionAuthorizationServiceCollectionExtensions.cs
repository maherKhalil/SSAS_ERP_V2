using Microsoft.AspNetCore.Authorization;

namespace SSAS.Host.API.Authorization;

public static class PermissionAuthorizationServiceCollectionExtensions
{
  public static IServiceCollection AddHostPermissionAuthorization(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddAuthorization();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
    services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
    services.AddScoped<LiveTenantEligibilityAuthorization>();

    return services;
  }
}
