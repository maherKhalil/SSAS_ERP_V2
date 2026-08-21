using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Application.Departments;

// SERIALISES EVERY CHANGE TO ONE COMPANY'S DEPARTMENT HIERARCHY (FP-007 Phase 2, ADR-026 decision 4).
//
// ================================================================================================
// WHY A PRE-CHECK IS NOT ENOUGH, AND THIS IS.
// ================================================================================================
//
// The acyclicity rule is validated by walking upward from the proposed parent. That walk is a READ, and the
// re-parent is a WRITE, so there is a gap between them — and two moves that are each individually legal can
// combine through that gap into a cycle:
//
//   A and B are both roots.
//   Tx1 validates "A under B": walks up from B, reaches a root, finds no A. Legal.
//   Tx2 validates "B under A": walks up from A, reaches a root, finds no B. Legal.
//   Both commit. A is now under B and B is under A.
//
// Neither transaction read anything stale — each was correct when it was taken. Retrying does not help,
// because there was never a conflict to detect: the two rows they wrote are different rows, so row-level
// optimistic concurrency sees nothing wrong. What is missing is MUTUAL EXCLUSION, which is what this is.
//
// ---- WHY NOT SERIALIZABLE ISOLATION.
//
// It would work, and it would make an invisible property of the transaction responsible for a named
// business invariant. A reader asking "what stops the A/B interleaving?" would have to know both that the
// isolation level is raised and why that closes it. An explicit lock says what it protects.
//
// ---- WHAT THE IMPLEMENTATION MUST GUARANTEE.
//
// It must work ACROSS APPLICATION INSTANCES — an in-process lock closes nothing on a second node — and it
// must be held from before the ancestry read until after the commit, or the gap simply moves. The key is
// derived from tenant AND company, never wider: two companies' hierarchies are independent, and locking a
// whole tenant would serialise unrelated administration.
//
// ---- FAILURE TO ACQUIRE IS A DEFINED REFUSAL.
//
// Never a silent continue. `DepartmentErrors.HierarchyMutationBusy` is distinct from every other refusal
// here precisely because it is the one that is transient and worth retrying.
public interface IDepartmentHierarchyLock
{
  // Taken INSIDE the caller's transaction and released by its commit or rollback. The caller must already
  // have opened the transaction this participates in; acquiring outside it would leave the same gap this
  // exists to close.
  Task<Result> AcquireAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}
