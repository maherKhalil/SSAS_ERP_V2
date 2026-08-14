using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Trusted tenant -> route resolution (ADR-017).
//
// NO CACHE by design in this slice. Every call reads current registry state, which makes correctness
// trivial to reason about and lets RoutingVersion semantics be proven before any caching exists. When a
// cache is eventually added, a cached entry is valid only for the RoutingVersion it was resolved under;
// invalidation and a bounded TTL are propagation aids, not the correctness mechanism (ADR-020).
//
// Every failure path refuses to route. None of them substitutes another database.
public sealed class TenantDatabaseResolver(ITenantDatabaseRegistryReadRepository repository) : ITenantDatabaseResolver
{
  public async Task<Result<TenantDatabaseRoute>> ResolveAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.TenantContextMissing);
    }

    var assignment = await repository.FindActiveAssignmentAsync(tenantId, cancellationToken);
    if (assignment is null)
    {
      // No active assignment: the tenant is simply not routable. Falling back to the Platform database or
      // to any other tenant database here would cross a placement boundary (ADR-017).
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.ActiveAssignmentMissing);
    }

    // CustomerManaged is architecture-ready in the schema but has no runtime path: no endpoint, no
    // credential reference, no connectivity implementation exists (ADR-021 is implementation-deferred).
    // Rejecting it explicitly is what prevents it being silently treated as platform-managed.
    if (assignment.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.UnsupportedHostingMode);
    }

    // ProvisioningStatus is the only lifecycle dimension that exists yet. The ADR-018 health dimensions
    // (connectivity, schema compatibility, migration execution) are NOT consulted here because nothing
    // maintains them; that gating arrives with the health slice.
    if (assignment.ProvisioningStatus != TenantDatabaseProvisioningStatus.Ready)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.TenantDatabaseNotReady);
    }

    if (string.IsNullOrWhiteSpace(assignment.ServerKey))
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.ServerKeyNotConfigured);
    }

    if (string.IsNullOrWhiteSpace(assignment.DatabaseName) ||
      assignment.DatabaseName.Length > TenantDatabase.DatabaseNameMaximumLength)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.DatabaseNameInvalid);
    }

    return Result.Success(new TenantDatabaseRoute(
      assignment.TenantId,
      assignment.TenantDatabaseId,
      assignment.ServerKey,
      assignment.DatabaseName,
      assignment.HostingMode,
      assignment.StorageMode,
      assignment.RoutingVersion));
  }
}
