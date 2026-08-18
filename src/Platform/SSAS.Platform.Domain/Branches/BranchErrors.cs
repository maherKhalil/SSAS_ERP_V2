using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Branches;

// NOTHING HERE NAMES DATABASE TOPOLOGY. Branch lives in the tenant database and UserBranchAccess in the
// platform one, and a caller told which is which learns the shape of the estate from an error message.
public static class BranchErrors
{
  public static readonly Error InvalidBranchCode = new("Branch.InvalidCode", "The branch code is invalid.");
  public static readonly Error InvalidBranchName = new("Branch.InvalidName", "The branch name is invalid.");
  public static readonly Error InvalidActor = new("Branch.InvalidActor", "A trusted lifecycle actor is required.");

  public static readonly Error NotFound = new("Branch.NotFound", "The branch was not found.");
  public static readonly Error Inactive = new("Branch.Inactive", "The branch is not active.");
  public static readonly Error CodeAlreadyExists =
    new("Branch.CodeAlreadyExists", "The branch code already exists within the tenant.");
  public static readonly Error MainBranchAlreadyExists =
    new("Branch.MainBranchAlreadyExists", "The tenant already has an active main branch.");
  public static readonly Error AlreadyInactive =
    new("Branch.AlreadyInactive", "The branch is already inactive.");

  // ---- ONBOARDING AND SELECTION.
  public static readonly Error FirstBranchRequired =
    new("Branch.FirstBranchRequired", "The tenant has no active branch; an administrator must create the first one.");
  public static readonly Error SelectionRequired =
    new("Branch.SelectionRequired", "An active branch must be selected before branch-scoped operations.");
  public static readonly Error InvalidSelection =
    new("Branch.InvalidSelection", "The selected branch is not available to this user.");
  public static readonly Error ContextRequired =
    new("Branch.ContextRequired", "A trusted branch context is required for branch-owned data.");

  // ---- ASSIGNMENT INVARIANTS.
  //
  // ONE GENERIC ERROR FOR EVERY BAD BRANCH REFERENCE. "Does not exist", "belongs to another tenant" and
  // "is inactive" are answered identically on purpose: distinguishing them would let a tenant administrator
  // probe another tenant's branch identifiers for existence.
  public static readonly Error UserMustHaveAtLeastOneBranch =
    new("Branch.UserMustHaveAtLeastOneBranch", "A tenant user must be authorized for at least one active branch.");
  public static readonly Error AssignmentInvalid =
    new("Branch.AssignmentInvalid", "One or more requested branches are not assignable.");
  public static readonly Error DeactivationWouldStrandUsers =
    new("Branch.DeactivationWouldStrandUsers", "Deactivating the branch would leave a user with no active branch.");

  // ---- LIFECYCLE (B1a).
  //
  // A tenant that has finished onboarding must keep at least one active branch: zero is a provisioning
  // state, not something an administrator may return to by deactivating the last one.
  public static readonly Error CannotDeactivateOnlyActiveBranch =
    new("Branch.CannotDeactivateOnlyActiveBranch", "The tenant's only active branch cannot be deactivated.");

  // Deactivating the active main branch requires naming its successor in the same operation, so the tenant
  // is never left with active branches and no main among them.
  public static readonly Error ReplacementMainBranchRequired =
    new("Branch.ReplacementMainBranchRequired", "Deactivating the main branch requires a replacement main branch.");

  public static readonly Error ConcurrencyConflict =
    new("Branch.ConcurrencyConflict", "The branch was modified concurrently; reload and retry.");

  // The branch topology is being changed by someone else right now. Distinct from a concurrency conflict:
  // nothing was attempted and nothing was lost, so the caller should simply retry.
  public static readonly Error TopologyBusy =
    new("Branch.TopologyBusy", "Another branch administration operation is in progress for this tenant.");

  // A normal user with no reachable branch. B1b's invariant makes this unreachable through supported
  // workflows, so it means the account has been left in a state it should not be in — refused rather than
  // presented as an empty branch picker.
  public static readonly Error AccountIntegrityFailure =
    new("Branch.AccountIntegrityFailure", "The account has no active branch and cannot be used.");

  public static readonly Error TenantAdministratorRequired =
    new("Branch.TenantAdministratorRequired", "Tenant administrator authority is required to administer branches.");
}
