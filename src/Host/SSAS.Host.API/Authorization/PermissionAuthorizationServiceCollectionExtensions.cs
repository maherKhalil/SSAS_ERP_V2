using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAS.Platform.Application.Permissions;

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

    // Platform-plane authorization (ADR-015 §8): stateless, catalog-driven. The code-owned catalog is required by
    // the platform handler; TryAdd keeps it available wherever authorization is wired (the persistence layer also
    // registers the same singleton) without duplicating or overriding it.
    //
    // COMPOSED, not Platform-only (FP-006P, ADR-012 r1.2): the catalog the role-assignment path validates
    // against must be the one authorization reads, or a module permission could be grantable and then
    // unrecognised, or the reverse.
    services.TryAddSingleton<PlatformPermissionCatalog>();
    services.TryAddSingleton<IPermissionCatalog, ComposedPermissionCatalog>();
    services.AddScoped<IAuthorizationHandler, PlatformPermissionAuthorizationHandler>();

    return services;
  }
}
