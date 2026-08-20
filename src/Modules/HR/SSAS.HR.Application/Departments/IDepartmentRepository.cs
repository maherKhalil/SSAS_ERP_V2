using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments;

// The Department aggregate's own repository (ADR-010).
//
// AGGREGATE-SPECIFIC, WITH NO GENERIC SURFACE: no deferred query type crosses this boundary, there is no
// generic repository, and there is deliberately NO DELETE METHOD — physical Department deletion is
// prohibited and the absence of a method is the first of the two protections. The second is the RESTRICTED
// foreign keys from `DepartmentManagers` and `EmployeeDepartmentAssignments`.
//
// ---- IT IS SMALL ON PURPOSE.
//
// Phase 1 adds only what Phase 1 can prove. Ancestry traversal, scoped reads and the manager and history
// queries all belong to later phases, and each needs a shape decided alongside the handler that consumes
// it. A method added now with no caller is a method whose signature is guessed rather than driven — and
// `ADR-026` decision 4 specifically requires the ancestry method to return evidence the aggregate cannot
// fabricate, which is a design that must arrive with `ChangeParent`'s Phase 2 signature, not before it.
//
// Uniqueness is TESTED here and ENFORCED by the database. The per-company unique index is authoritative
// under concurrent creation; this check exists so the common case returns a named conflict rather than a
// raw persistence failure, not because it is the rule.
public interface IDepartmentRepository
{
  // Within the trusted tenant and the caller's authorized company scope. Returns null rather than an error
  // so the caller can decide whether "not found" is a refusal or an ordinary absence.
  Task<Department?> GetByIdAsync(Guid departmentId, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  // The same question, excluding one department — needed when RENAMING, where the department's own existing
  // code must not count as a conflict with itself.
  Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedDepartmentId,
    CancellationToken cancellationToken = default);

  Task AddAsync(Department department, CancellationToken cancellationToken = default);

  // ---- THE ANCESTRY WALK (ADR-026 decision 4, BR-HR-0008).
  //
  // Returns the chain from the given department UP to its root, the department itself first. The direction
  // matters: walking up from the PROPOSED PARENT is O(depth), while walking down over descendants of the
  // department being moved is O(subtree).
  //
  // Read INSIDE the caller's transaction, under the hierarchy lock, so the chain it returns is the chain the
  // write commits against. A cached or no-tracking-outside-transaction answer would reintroduce the gap the
  // lock closes.
  Task<IReadOnlyList<Department>> GetAncestryAsync(
    Guid departmentId, CancellationToken cancellationToken = default);

  // Whether any ACTIVE child references this department. Inactive children do not block deactivation.
  Task<bool> HasActiveChildrenAsync(
    Guid departmentId, CancellationToken cancellationToken = default);

  // ---- THE MANAGER ASSOCIATION AND THE HISTORY LIVE HERE, NOT IN REPOSITORIES OF THEIR OWN.
  //
  // Neither is an aggregate root: `DepartmentManager` is keyed by the department and is meaningless without
  // it, and the department history is written as a consequence of a department change. Giving each its own
  // repository would be proliferation without precedent — FP-006 kept `AppendBranchAssignmentAsync` on
  // `IEmployeeRepository` for exactly this reason, and this follows that precedent.
  Task<DepartmentManager?> GetManagerAsync(
    Guid departmentId, CancellationToken cancellationToken = default);

  Task SetManagerAsync(DepartmentManager manager, CancellationToken cancellationToken = default);

  // Removes the association, which is what "this department has no manager" means. It is NOT a department
  // deletion and does not weaken the no-physical-delete rule, which governs departments.
  Task ClearManagerAsync(DepartmentManager manager, CancellationToken cancellationToken = default);

  // APPEND ONLY. There is no update and no remove counterpart, here or anywhere: a correction is another
  // department change, never a rewrite.
  Task AppendDepartmentAssignmentAsync(
    EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken = default);
}
