using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.TenantStorage;

// Resolves a TRUSTED tenant identity to trusted routing metadata (ADR-017).
//
// The tenant id is an explicit parameter rather than an ambient lookup: this resolver must be equally
// usable from a request (which passes ICurrentTenant's validated value) and from a background worker
// (which establishes tenant identity explicitly). Depending on IHttpContextAccessor here would make it
// silently unusable off the request path — the exact trap ADR-017 warns about for tenant-less access.
//
// It FAILS CLOSED. There is no route for a tenant without an active assignment, for a database that is not
// ready, or for a hosting mode that is not implemented. It never substitutes another database, the Platform
// database, or a default, because placement is a security and data-residency boundary (ADR-017).
public interface ITenantDatabaseResolver
{
  Task<Result<TenantDatabaseRoute>> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
