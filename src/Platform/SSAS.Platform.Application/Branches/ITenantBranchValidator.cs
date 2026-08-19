using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Branches;

// VALIDATES REQUESTED BRANCH IDENTIFIERS AGAINST THE TENANT DATABASE (Branch foundation B1b).
//
// A branch identifier arriving from a client is a claim, not a fact. It may name a branch that never
// existed, one that belongs to a different tenant, or one that was deactivated while the administrator was
// filling in the form — and the platform database, where the assignment row is about to be written, cannot
// tell the difference because it holds no branch rows.
//
// READ-ONLY, AND ON THE TENANT PLANE. User creation never writes to the tenant database; it only asks it
// whether these branches are assignable right now.
//
// CALLED UNDER THE TOPOLOGY LEASE, always. Outside it the answer is immediately stale: a deactivation
// committing a moment later turns a validated branch into an invalid one before the assignment lands.
public interface ITenantBranchValidator
{
  // ONE ANSWER FOR THE WHOLE SET, and one generic failure for every way a branch can be unassignable —
  // absent, foreign, or inactive. Distinguishing them would let an administrator of one tenant probe
  // another tenant's identifiers for existence.
  Task<Result> ValidateAssignableAsync(
    Guid tenantId,
    IReadOnlyCollection<Guid> branchIds,
    CancellationToken cancellationToken = default);
}
