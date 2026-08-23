using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;

namespace SSAS.GL.Infrastructure;

// GL'S PERSISTENCE COMPOSITION (ADR-012 r1.2).
//
// The Host is the ONE place permitted to see a module's Infrastructure, and module registration is
// EXPLICIT — never discovered by reflection. A module that is not registered here contributes nothing:
// its entities are absent from the tenant model, absent from the migration stream, and absent from
// Shared to Dedicated cutover. The last of those fails silently, which is the whole reason the
// contributor set is a registration rather than a scan.
public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGlInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // Singleton, matching HR: the contributor is stateless and deterministic, and it participates in the
    // EF model cache key. A scoped contributor would be constructed per request for a model built once.
    services.AddSingleton<ITenantModelContributor, GlTenantModelContributor>();

    return services;
  }
}
