using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Branches;

// THE BRANCH HALF OF SIGNING IN (Branch foundation B1c).
//
// It runs AFTER authentication and tenant resolution, because branches live in the tenant's own database
// and cannot be enumerated until routing has resolved. It does not authenticate anything and does not
// create sessions — it decides which branch an already-authenticated session is working in.
public interface IBranchSessionService
{
  // Called once the session exists. Auto-selects when the answer is unambiguous, and otherwise leaves the
  // session with no branch so that branch-scoped work stays refused until the user chooses.
  Task<Result<BranchSessionState>> ResolveForSessionAsync(
    long authenticationSessionId,
    CancellationToken cancellationToken = default);

  // SELECTION AND SWITCHING ARE THE SAME OPERATION. A switch is a selection made later, and giving them
  // separate paths would mean two places that must agree about what authorization means.
  Task<Result<BranchSessionState>> SelectActiveBranchAsync(
    long authenticationSessionId,
    Guid branchId,
    CancellationToken cancellationToken = default);
}

// What the caller must do next, and the data needed to do it.
public sealed record BranchSessionState(
  BranchSessionOutcome Outcome,
  Guid? ActiveBranchId,
  IReadOnlyList<BranchAccessSummary> SelectableBranches);

public enum BranchSessionOutcome
{
  // A branch is selected and branch-scoped work may proceed.
  Active = 1,

  // More than one branch is authorized. There is deliberately NO skip: the session stays branch-less and
  // every branch-owned write is refused until a choice is made.
  BranchSelectionRequired = 2,

  // A tenant administrator whose tenant has no branches yet. The onboarding path, not an error.
  FirstBranchRequired = 3
}
