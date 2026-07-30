using Microsoft.AspNetCore.Authorization;

namespace SSAS.Host.API.Authorization;

public static class PermissionAuthorizationServiceCollectionExtensions
{
  public static IServiceCollection AddHostPermissionAuthorization(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddAuthorization();
    services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
    services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

    return services;
  }
}
