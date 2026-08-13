namespace SSAS.Platform.Application.TenantStorage;

// Tenant-storage registry bootstrap (ADR-017). Establishes the registry baseline for the existing
// single-database deployment: one TenantDatabase row describing the current physical database, and one
// active assignment per existing tenant pointing at it.
//
// Idempotent by design — a second run must change nothing — because it runs on every host start and may
// run concurrently on several hosts. It never repairs contradictory state silently.
public interface ITenantStorageBootstrapService
{
  Task<TenantStorageBootstrapOutcome> RunAsync(CancellationToken cancellationToken = default);
}
