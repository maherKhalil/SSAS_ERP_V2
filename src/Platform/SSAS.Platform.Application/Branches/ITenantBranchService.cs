using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Branches;

// THE BRANCH LIFECYCLE (Branch foundation B1a).
//
// EVERY OPERATION IS SCOPED TO THE CURRENT TENANT CONTEXT and requires Platform.Tenant.Administer. The
// tenant is never taken from the caller: a branch identifier plus a client-supplied tenant would let an
// administrator of one tenant name another tenant's branch and have the service agree.
//
// THERE IS NO DELETE, and there will not be one. A branch identifier is referenced from the platform
// database and from every document produced while it was active; removing the row would strand both and
// make historical data unexplainable. Deactivation is the whole of the retirement story.
public interface ITenantBranchService
{
  Task<Result<BranchDto>> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

  Task<Result<BranchDto>> GetAsync(Guid branchId, CancellationToken cancellationToken = default);

  // includeInactive exists for the administration view, which must be able to explain a branch that appears
  // on old documents but is no longer selectable.
  Task<Result<IReadOnlyList<BranchDto>>> ListAsync(
    bool includeInactive = false,
    CancellationToken cancellationToken = default);

  Task<Result<BranchDto>> UpdateAsync(UpdateBranchRequest request, CancellationToken cancellationToken = default);

  Task<Result> DeactivateAsync(DeactivateBranchRequest request, CancellationToken cancellationToken = default);

  // ---- THE ONBOARDING PRIMITIVE (B1a), consumed by the login flow in B1c.
  //
  // It answers only "has this tenant finished branch onboarding". Deliberately NOT a login decision: this
  // slice does not know about sessions, and folding the two together would put an authentication concern
  // inside branch administration.
  Task<Result<TenantBranchOnboardingState>> GetOnboardingStateAsync(CancellationToken cancellationToken = default);
}

public sealed record BranchDto(
  Guid BranchId,
  string BranchCode,
  string BranchName,
  bool IsMainBranch,
  bool IsActive,
  byte[] RowVersion);

public sealed record CreateBranchRequest(string? Code, string? Name, bool IsMainBranch);

// RowVersion is REQUIRED, not optional. An update that omitted it would be a last-writer-wins edit of a
// record two administrators can hold open at once.
public sealed record UpdateBranchRequest(
  Guid BranchId,
  string? Code,
  string? Name,
  bool IsMainBranch,
  byte[] RowVersion);

// ReplacementMainBranchId is required only when retiring the active main branch; naming it here rather than
// choosing one automatically keeps the decision with the administrator who knows the business.
public sealed record DeactivateBranchRequest(
  Guid BranchId,
  Guid? ReplacementMainBranchId,
  byte[] RowVersion);

public sealed record TenantBranchOnboardingState(bool FirstBranchRequired, int ActiveBranchCount);
