using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.PlatformSupport;

namespace SSAS.Platform.Application.Abstractions.Queries;

// Read-only projections over global platform-support authority for administration (DEC-TEN-0025, Phase 4C).
// All reads are AsNoTracking, server-side paged/bounded, EF-entity-free, and carry no tenant scope. Effective
// active-permission projection is not here — it reuses IPlatformSupportPermissionReadService (catalog-filtered).
public interface IPlatformSupportAuthorityReadService
{
  Task<PagedResult<PlatformSupportPrincipalDto>> ListPrincipalsAsync(
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default);

  Task<PlatformSupportPrincipalDto?> GetPrincipalAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default);

  // Full assignment history (active + revoked), most-recent-first with a stable Id tie-breaker. Returns null
  // when the principal does not exist (distinct from an existing principal with an empty history).
  Task<IReadOnlyList<PlatformPermissionAssignmentDto>?> ListAssignmentsAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default);

  Task<bool> PrincipalExistsAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default);
}
