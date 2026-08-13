using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// Read-only platform-support authority projections (DEC-TEN-0025, Phase 4C). Every read is AsNoTracking and
// bounded/server-side paged; nothing joins tenant data or exposes EF entities/secrets. The immutable primary
// key (Id) is the deterministic order/tie-breaker so repeated reads over unchanged data are stable.
public sealed class PlatformSupportAuthorityReadService(PlatformDbContext dbContext)
  : IPlatformSupportAuthorityReadService
{
  public async Task<PagedResult<PlatformSupportPrincipalDto>> ListPrincipalsAsync(
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default)
  {
    var query = dbContext.PlatformSupportPrincipals.AsNoTracking();
    var totalCount = await query.CountAsync(cancellationToken);
    var principals = await query
      .OrderBy(principal => principal.Id)
      .Skip((pageNumber - 1) * pageSize)
      .Take(pageSize)
      .ToArrayAsync(cancellationToken);
    return new PagedResult<PlatformSupportPrincipalDto>(
      principals.Select(MapPrincipal).ToArray(), pageNumber, pageSize, totalCount);
  }

  public async Task<PlatformSupportPrincipalDto?> GetPrincipalAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default)
  {
    var principal = await dbContext.PlatformSupportPrincipals.AsNoTracking()
      .SingleOrDefaultAsync(item => item.Id == platformSupportPrincipalId, cancellationToken);
    return principal is null ? null : MapPrincipal(principal);
  }

  public async Task<IReadOnlyList<PlatformPermissionAssignmentDto>?> ListAssignmentsAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default)
  {
    if (!await PrincipalExistsAsync(platformSupportPrincipalId, cancellationToken))
    {
      return null;
    }

    // Full history: active AND revoked/removed rows, most-recent-first with a stable Id tie-breaker. Never
    // filtered through the current catalog — a since-retired permission's historical row stays visible.
    var assignments = await dbContext.PlatformPermissionAssignments.AsNoTracking()
      .Where(assignment => assignment.PlatformSupportPrincipalId == platformSupportPrincipalId)
      .OrderByDescending(assignment => assignment.AssignedUtc)
      .ThenByDescending(assignment => assignment.Id)
      .ToArrayAsync(cancellationToken);
    return assignments.Select(MapAssignment).ToArray();
  }

  public Task<bool> PrincipalExistsAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals.AsNoTracking()
      .AnyAsync(item => item.Id == platformSupportPrincipalId, cancellationToken);

  private static PlatformSupportPrincipalDto MapPrincipal(PlatformSupportPrincipal principal) => new(
    principal.Id,
    principal.IdentityId,
    principal.Status,
    principal.CreatedUtc,
    principal.CreatedBy,
    principal.ModifiedUtc,
    principal.ModifiedBy,
    principal.StatusChangedUtc,
    principal.StatusChangedBy,
    principal.RowVersion.ToArray());

  private static PlatformPermissionAssignmentDto MapAssignment(PlatformPermissionAssignment assignment) => new(
    assignment.Id,
    assignment.PlatformSupportPrincipalId,
    assignment.PermissionName.Value,
    assignment.AssignedUtc,
    assignment.AssignedBy,
    assignment.RemovedUtc,
    assignment.RemovedBy,
    assignment.IsActive);
}
