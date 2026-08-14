using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// Registry read for routing (ADR-017). One join, one row, projected to a flat record so no EF entity
// escapes into the Application layer.
//
// The registry is Platform operational metadata and neither entity is ITenantOwnedEntity, so no tenant
// global filter applies and this works without an ambient tenant — which is required, because the caller
// is resolving routing FOR a tenant rather than operating inside one.
public sealed class TenantDatabaseRegistryReadRepository(PlatformDbContext dbContext)
  : ITenantDatabaseRegistryReadRepository
{
  public Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default) =>
    // SingleOrDefaultAsync, deliberately not FirstOrDefaultAsync: UX_TenantDatabaseAssignments_ActiveTenant
    // makes two active assignments structurally impossible, so if one ever appears (corruption, or a
    // disabled constraint) this throws rather than silently picking a database. Choosing would be the worst
    // possible outcome — it would route a tenant's data somewhere arbitrary.
    dbContext.TenantDatabaseAssignments
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == tenantId && assignment.EndedUtc == null)
      .Join(
        dbContext.TenantDatabases.AsNoTracking(),
        assignment => assignment.TenantDatabaseId,
        database => database.Id,
        (assignment, database) => new TenantDatabaseAssignmentRecord(
          assignment.TenantId,
          assignment.TenantDatabaseId,
          assignment.RoutingVersion,
          database.ServerKey,
          database.DatabaseName,
          database.HostingMode,
          database.StorageMode,
          database.ProvisioningStatus))
      .SingleOrDefaultAsync(cancellationToken);
}
