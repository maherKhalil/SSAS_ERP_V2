using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// "Has this tenant's routing moved?" — one column, one row (ADR-020).
//
// THE QUERY SHAPE IS THE POINT: a seek on `UX_TenantDatabaseAssignments_ActiveTenant`, the existing unique
// index keyed on TenantId and filtered to `EndedUtc IS NULL`, returning at most one row. No join to the
// registry, no projection of health or migration state. That index already exists for the assignment
// invariant, so this read adds no index and no write cost.
//
// SingleOrDefault rather than FirstOrDefault, matching the assignment repository: two active assignments
// are structurally impossible, and if one ever appeared, throwing beats silently choosing a version — and
// therefore a database.
public sealed class TenantRoutingVersionReader(PlatformDbContext dbContext) : ITenantRoutingVersionReader
{
  public async Task<Result<long>> ReadCurrentRoutingVersionAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<long>(TenantStorageErrors.TenantContextMissing);
    }

    long? version;
    try
    {
      version = await dbContext.TenantDatabaseAssignments
        .AsNoTracking()
        .Where(assignment => assignment.TenantId == tenantId && assignment.EndedUtc == null)
        .Select(assignment => (long?)assignment.RoutingVersion)
        .SingleOrDefaultAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception)
    {
      // UNAVAILABLE IS NOT UNCHANGED. Returning a failure is what forces the caller to refuse rather than
      // fall back to a remembered route, which during a cutover would write to the wrong database.
      return Result.Failure<long>(TenantStorageErrors.RoutingVersionUnavailable);
    }

    return version is { } current
      ? Result.Success(current)
      : Result.Failure<long>(TenantStorageErrors.ActiveAssignmentMissing);
  }
}
