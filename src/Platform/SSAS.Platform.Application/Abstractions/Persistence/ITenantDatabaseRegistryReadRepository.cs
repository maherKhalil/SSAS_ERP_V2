using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Narrow read access to the tenant-storage registry for routing (ADR-017). Deliberately minimal: routing
// needs exactly one lookup, so this is not a general registry repository.
public interface ITenantDatabaseRegistryReadRepository
{
  // Returns the tenant's ACTIVE assignment joined to its physical database, or null when none exists.
  // Implementations must fail rather than choose when more than one active assignment is somehow present.
  Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
