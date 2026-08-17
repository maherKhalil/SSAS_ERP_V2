using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Branches;

// WHICH BRANCHES A USER MAY ENTER, AND THE ONLY PLACE THAT DECIDES IT (Branch foundation B0/B1).
//
// TWO SOURCES OF SCOPE, ONE ANSWER. A tenant administrator's scope is every ACTIVE branch in the tenant,
// derived from authority; everyone else's is their UserBranchAccess rows intersected with the branches that
// are still active. Both are resolved here so no caller has to remember which rule applies — a login path
// and a write path disagreeing about that is precisely how a user ends up able to write somewhere they
// cannot see, or vice versa.
//
// IT ALWAYS INTERSECTS WITH ACTIVE BRANCHES. An assignment row naming a deactivated branch is not access:
// the row survives deactivation deliberately, so that reactivating a branch restores the access that
// existed before it, and filtering here is what stops that retained row from granting entry meanwhile.
public interface ITenantBranchAccessResolver
{
  // Every branch this user may currently enter, active only. Empty is a legitimate answer for a tenant
  // administrator whose tenant has no branches yet; for a normal user it means the account is unusable and
  // the caller must fail closed rather than fall back to "all".
  Task<Result<IReadOnlyList<BranchAccessSummary>>> GetPermittedBranchesAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default);

  // THE AUTHORITATIVE SINGLE-BRANCH CHECK, re-asked at selection, at switching, and at every branch-owned
  // write. Deliberately NOT answered from a list captured at login: access can be revoked and a branch can
  // be deactivated inside a session's lifetime, and a write admitted on a stale list is the failure this
  // exists to prevent.
  Task<Result> AuthorizeBranchAsync(
    Guid tenantId,
    long tenantUserId,
    Guid branchId,
    CancellationToken cancellationToken = default);
}

// What a caller needs to render a branch picker or auto-select, and nothing more.
public sealed record BranchAccessSummary(Guid BranchId, string BranchCode, string BranchName, bool IsMainBranch);
