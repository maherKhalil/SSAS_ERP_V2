using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Tenancy.Branches;

// THE TRUSTED BRANCH THIS OPERATION IS WORKING IN (FP-006C3, ADR-023).
//
// ---- WHY A MODULE NEEDS THIS AT ALL.
//
// A branch-owned entity never has to ask: the write boundary stamps its BranchId from the execution context
// during save. But a module sometimes has to RECORD which branch an operation happened in on something that
// is not itself branch-owned — Employee's initial branch-assignment row is exactly that case, since the
// history spans branches and so belongs to none of them (ADR-024 decision 4).
//
// ---- IT IS A READ OF THE SAME ANSWER THE BOUNDARY USES, NOT A SECOND OPINION.
//
// The implementation delegates to the branch write authorizer, so the branch a module records and the branch
// the boundary stamps are the same value from the same source. Two independent resolutions is precisely how
// a recorded history comes to disagree with the record it describes.
//
// IT IS NOT AUTHORIZATION TO DO ANYTHING. It answers "which branch", re-read from authoritative state, and
// the write boundary still re-asks and still fails closed on every save. A module holding this value has
// gained no permission it did not already have.
//
// ASYNC BECAUSE THE ANSWER IS A DATABASE QUESTION — the durable session and the live access resolver — which
// is the same reason the write boundary uses an authorizer rather than a synchronous ICurrentBranch.
public interface ICurrentBranchResolver
{
  // Fails closed: no branch selected, session unusable, access revoked, authority revoked or branch
  // deactivated each return a failure rather than a branch.
  Task<Result<Guid>> ResolveCurrentBranchAsync(CancellationToken cancellationToken = default);
}
