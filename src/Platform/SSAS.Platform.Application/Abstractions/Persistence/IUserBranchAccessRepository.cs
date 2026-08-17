using SSAS.Platform.Domain.Branches;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// The platform-side branch assignment rows (Branch foundation B1b).
//
// REPLACE-SET SEMANTICS, PHYSICAL. B0 modelled UserBranchAccess as a live capability list with no lifecycle
// columns, so removing access is deleting the row — the established model for this entity, and changing it
// would mean a schema change nobody has asked for. The audit trail of who could reach where belongs to the
// platform audit stream, not to rows that every authorization query would then have to filter out.
public interface IUserBranchAccessRepository
{
  Task<IReadOnlyList<Guid>> GetBranchIdsAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default);

  Task AddAsync(UserBranchAccess access, CancellationToken cancellationToken = default);

  // Removes exactly the named assignments. Called with the difference between the current and desired sets
  // so a replace touches only what actually changed.
  Task RemoveAsync(
    Guid tenantId,
    long tenantUserId,
    IReadOnlyCollection<Guid> branchIds,
    CancellationToken cancellationToken = default);
}
