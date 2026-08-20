using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments.Reads;

// WHAT A DEPARTMENT LOOKS LIKE TO A READER (FP-007 Phase 2).
//
// Ownership identifiers are carried because the existing Employee read models carry theirs, and a caller
// that cannot tell which company a department belongs to cannot render a multi-company list.
public sealed record DepartmentDetail(
  Guid DepartmentId,
  Guid CompanyId,
  string Code,
  string Name,
  Guid? ParentDepartmentId,
  DepartmentStatus Status,
  // Who is RECORDED as the manager, straight from the association. The read service fills this; it is not
  // part of the caller-facing answer, and the handler replaces it with `Manager` once the employee scope
  // has decided how much of that person may be disclosed.
  Guid? ManagerEmployeeId,
  DepartmentManagerSummary? Manager,
  byte[] RowVersion);

// The lighter shape for lists. No manager: resolving one per row would turn a paged list into N employee
// lookups, and a search result does not need it.
public sealed record DepartmentListItem(
  Guid DepartmentId,
  Guid CompanyId,
  string Code,
  string Name,
  Guid? ParentDepartmentId,
  DepartmentStatus Status,
  byte[] RowVersion);

// ================================================================================================
// THE MANAGER SUMMARY ANSWERS TWO QUESTIONS THAT ARE NOT THE SAME QUESTION.
// ================================================================================================
//
// 1. IS SOMEBODY ASSIGNED? A department-scoped fact, answered from `DepartmentManagers`.
// 2. WHO ARE THEY, AND ARE THEY STILL VALID? An EMPLOYEE fact, and employee facts are branch-scoped.
//
// ---- WHY THE SECOND ONE CANNOT SIMPLY BE JOINED IN.
//
// A department is COMPANY-visible: a caller authorized only for Riyadh reads the Finance department that
// spans Riyadh and Jeddah. Its manager may work in Jeddah — an employee that caller has no scope for. A
// join from the department read would hand out that person's name and number on the strength of department
// visibility alone, which is a branch-scope bypass wearing a department's clothes.
//
// So the details are resolved through the EXISTING employee read path, with its existing branch scope, and
// when that path cannot see the manager the summary says so instead of inventing a second authorization
// model. `IsAssigned` without details is a real and useful answer: the department HAS a head, and you are
// not authorized to know who.
//
// ---- AND "ASSIGNED" IS NOT "CURRENTLY VALID".
//
// A manager assignment is never cleared automatically when the employee is terminated — clearing it would
// destroy the record that there had been one, and would make a write to HR structure a side effect of an
// unrelated operation. `IsActive` false means somebody IS recorded and is not currently a valid manager:
// the department needs a new one, which is worth surfacing rather than hiding behind a null.
//
// ---- IT CARRIES NO BRANCH, EVER.
//
// Even to a caller who may see the manager, the branch is not part of this summary. It is membership data,
// and it belongs to the employee surface.
public sealed record DepartmentManagerSummary(
  bool IsAssigned,
  Guid? EmployeeId,
  string? EmployeeNumber,
  string? FullName,
  bool IsActive)
{
  // Somebody is recorded, and this caller may not know who. Deliberately carries no identifier at all: an
  // opaque employee id is still a fact about an employee outside the caller's scope.
  public static DepartmentManagerSummary Undisclosed() => new(true, null, null, null, false);
}

// The children of one department, in deterministic order. Adjacency, one level — not a recursive tree.
public sealed record DepartmentChild(
  Guid DepartmentId,
  string Code,
  string Name,
  DepartmentStatus Status);

// The caller's search intent. Every filter is optional; the SCOPE is not, and comes from the resolver.
public sealed record SearchDepartmentsQuery(
  DepartmentCompanyScopeMode CompanyScope = DepartmentCompanyScopeMode.CurrentCompany,
  string? SearchText = null,
  DepartmentStatus? Status = null,
  Guid? ParentDepartmentId = null,
  int Page = 1,
  int PageSize = 25);

// THE READ SIDE'S ONE ENTRY POINT (FP-007 Phase 2).
//
// EVERY METHOD REQUIRES A DepartmentReadScope. There is no overload without one, no default, and no way to
// fabricate a scope meaning "everything" — a read that omitted a scope predicate is not something a caller
// can express.
public interface IDepartmentReadService
{
  // Returns the department and the IDENTIFIER of whoever is recorded as its manager — never that person's
  // details. Turning the identifier into a name is an employee read, and it happens behind the employee
  // scope in `GetDepartmentQueryHandler`.
  Task<Result<DepartmentDetail>> GetAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default);

  // The raw association, for the handler to resolve. Separated from `GetAsync` so this read service never
  // touches the employee set at all — an architecture guard asserts that, because the moment it does, a
  // department read becomes a way around branch scope.
  Task<Guid?> GetManagerEmployeeIdAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default);

  Task<Result<PagedResult<DepartmentListItem>>> SearchAsync(
    DepartmentReadScope scope, SearchDepartmentsQuery query, CancellationToken cancellationToken = default);

  // REQ-HR-0101. One level of the adjacency model, company-scoped like everything else.
  Task<Result<IReadOnlyList<DepartmentChild>>> GetChildrenAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default);
}
