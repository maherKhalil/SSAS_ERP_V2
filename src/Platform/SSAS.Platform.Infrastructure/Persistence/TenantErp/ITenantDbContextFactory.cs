using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Creates a TenantDbContext bound to the tenant's currently-routed physical database (ADR-017).
//
// This is the "custom equivalent" ADR-017 permits in place of IDbContextFactory<TenantDbContext>, and the
// reasons are specific rather than stylistic:
//
//  - IDbContextFactory<T>.CreateDbContext() takes no tenant, so the tenant would have to come from ambient
//    state — the coupling TS-1C deliberately removed from the resolver.
//  - Its options are built ONCE at registration. A connection captured there would pin every tenant to
//    whichever tenant happened to be routed first (ADR-017 binding lifetime rule 2). Here the connection is
//    chosen at creation time, per call.
//  - Routing can fail for ordinary, expected reasons (no assignment, database not Ready, ServerKey not
//    configured). Result<T> makes those first-class instead of exceptions thrown from a factory that has no
//    vocabulary for them.
public interface ITenantDbContextFactory
{
  // Resolves routing for the given trusted tenant and returns a context bound to that database. The
  // caller owns the returned context and must dispose it; disposal also closes the connection.
  Task<Result<TenantDbContext>> CreateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
