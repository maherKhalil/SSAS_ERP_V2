using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Narrow read access to the tenant-storage registry for routing (ADR-017). Deliberately minimal: routing
// needs exactly one lookup, so this is not a general registry repository.
public interface ITenantDatabaseRegistryReadRepository
{
  // Returns the tenant's ACTIVE assignment joined to its physical database, or null when none exists.
  // Implementations must fail rather than choose when more than one active assignment is somehow present.
  Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(Guid tenantId, CancellationToken cancellationToken = default);

  // PHYSICAL database discovery for schema health and migration (ADR-018).
  //
  // Deliberately over TenantDatabase and never over TenantDatabaseAssignment: the migration unit is the
  // physical database, so one shared database is one target regardless of how many tenants it hosts.
  // Iterating assignments would check and migrate the same database once per tenant.
  //
  // Keyset paged on Id rather than OFFSET, so a growing estate does not degrade into a scan per page.
  Task<IReadOnlyList<TenantDatabaseDescriptor>> ListPhysicalDatabasesAsync(
    long afterId,
    int take,
    CancellationToken cancellationToken = default);
}
