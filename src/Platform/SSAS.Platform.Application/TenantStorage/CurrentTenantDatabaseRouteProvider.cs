using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Request-path convenience over ITenantDatabaseResolver: reads the TRUSTED tenant from ICurrentTenant and
// delegates. Kept deliberately thin so the resolver itself stays free of ambient context and usable from
// background workers.
//
// A missing tenant FAILS CLOSED. It is never translated into a null route, a default shared database, or
// the Platform connection — "no trusted tenant" must not be indistinguishable from "route to something".
public sealed class CurrentTenantDatabaseRouteProvider(
  ICurrentTenant currentTenant,
  ITenantDatabaseResolver resolver)
{
  public Task<Result<TenantDatabaseRoute>> ResolveCurrentAsync(CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
    {
      return Task.FromResult(Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.TenantContextMissing));
    }

    return resolver.ResolveAsync(tenantId, cancellationToken);
  }
}
