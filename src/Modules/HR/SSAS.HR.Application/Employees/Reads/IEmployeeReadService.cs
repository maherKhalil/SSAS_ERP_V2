using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// ==================================================================================================
// EVERY METHOD TAKES AN EmployeeReadScope. THERE IS NO OVERLOAD THAT DOES NOT.
// ==================================================================================================
//
// This is the ADR-023 decision 22 / ADR-025 decision 10 guarantee expressed as a TYPE rather than as a
// convention (FP-006C4). An unscoped employee read is not a mistake a reviewer has to catch, because it is
// not a call a developer can write: the scope parameter is required, the scope cannot be constructed outside
// the resolver, and the resolver refuses unless the functional permission and both authorization dimensions
// pass against live state.
//
// NO DEFERRED QUERY TYPE APPEARS ANYWHERE ON THIS SURFACE. One crossing the application boundary
// would let a caller append to — or strip from — the composed predicate after the scope had been applied,
// which is the same hole with more steps.
//
// WHY NO GLOBAL QUERY FILTER STANDS IN FOR THIS. A filter is invisible at the call site, silently removable
// with IgnoreQueryFilters, and single-valued — it cannot express "these three branches". The predicates are
// therefore written explicitly by the implementation, per ADR-025 decision 10.
public interface IEmployeeReadService
{
  // Scoped by identifier. A row outside the scope is reported as NOT FOUND rather than as forbidden: any
  // other answer would confirm that an employee with that identifier exists somewhere the caller cannot see.
  Task<EmployeeDetail?> GetEmployeeAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default);

  Task<PagedResult<EmployeeSummary>> SearchEmployeesAsync(
    EmployeeReadScope scope, EmployeeSearchCriteria criteria, CancellationToken cancellationToken = default);

  // ---- HISTORY IS REACHED THROUGH THE EMPLOYEE, NEVER DIRECTLY.
  //
  // EmployeeBranchAssignment is company-owned but NOT branch-owned — it names two branches and belongs to
  // neither — so a branch predicate cannot be written over it. Its scope is therefore inherited: the
  // implementation proves the EMPLOYEE is inside the scope first and returns null if not, and only then
  // loads that employee's assignments. A history-by-employee-id API without that step would be an unscoped
  // read of branch identifiers, which is exactly what a caller confined to one branch must not obtain.
  Task<IReadOnlyList<EmployeeBranchHistoryEntry>?> GetEmployeeBranchHistoryAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default);

  // The POSITION history, on identical terms and for the identical reason (FP-008 Phase 4, FR-POS-0212).
  // `EmployeePositionAssignment` is company-owned but NOT branch-owned — it names two positions and belongs
  // to neither — so its scope is inherited from the employee it describes: prove the employee is inside the
  // scope first, return null if not.
  Task<IReadOnlyList<EmployeePositionHistoryEntry>?> GetEmployeePositionHistoryAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default);

  // ---- HOW MANY EMPLOYEES HOLD A POSITION (FP-008 Phase 3, `api-contracts.md`).
  //
  // ================================================================================================
  // IT LIVES HERE, AND THAT PLACEMENT IS THE WHOLE DESIGN.
  // ================================================================================================
  //
  // `employeeCount` appears on the POSITION representation, so the obvious home would be the position read
  // side. It is here instead because counting employees is an EMPLOYEE read, and employees are
  // branch-scoped while positions are only company-scoped. A count taken on the position side would either
  // need a second branch authorization model or would disclose the size of branches the caller cannot read
  // — the same trap `DepartmentReadService` refuses when it declines to join its manager.
  //
  // Requiring an `EmployeeReadScope` is what makes that structural: the count is filtered by the caller's
  // own company and branch predicate, so two users legitimately see different numbers for one position, and
  // the architecture guard asserting that no position read service reaches the employee set stays true.
  //
  // Phase 4 composes this into the position representation at the API layer, where both scopes are
  // obtainable — alongside `currencyCode`, which is the same shape of problem across a module boundary.
  Task<int> CountEmployeesByPositionAsync(
    EmployeeReadScope scope, Guid positionId, CancellationToken cancellationToken = default);
}

// WHAT A SEARCH FILTERS ON, beyond the scope. None of these can widen a scope; they only narrow it.
public sealed record EmployeeSearchCriteria(
  int PageNumber = EmployeeSearchCriteria.DefaultPageNumber,
  int PageSize = EmployeeSearchCriteria.DefaultPageSize,
  // Exact match on the NORMALIZED employee number, not a prefix or contains — the number is an identifier,
  // and it is unique per company rather than per tenant, so a cross-company search may legitimately return
  // more than one row.
  string? EmployeeNumber = null,
  // Omitted means Active AND Inactive — both are current employment. Terminated is excluded unless asked
  // for by name, so a routine list does not quietly include people who have left.
  IReadOnlyCollection<EmployeeStatus>? Statuses = null,
  // ---- FP-007 PHASE 3. A NARROWING FILTER, LIKE EVERY OTHER ONE HERE.
  //
  // A department spans the branches of its company, so naming one asks about employees across several
  // branches — but the scope's branch predicate is applied REGARDLESS and independently, so a caller
  // confined to Riyadh who filters by a company-wide department still sees only Riyadh's members. The
  // filter cannot reach the employees it does not already authorize; it can only remove more of them.
  //
  // A department in another company matches nothing rather than being refused, because the company
  // predicate has already excluded its employees — so this cannot be used to probe for departments either.
  Guid? DepartmentId = null)
{
  public const int DefaultPageNumber = 1;

  public const int DefaultPageSize = 50;

  // A HARD CEILING, not a suggestion. Page size is caller-supplied and the result set is the widest read in
  // the module; an unbounded one is a denial-of-service vector against the tenant database.
  public const int MaxPageSize = 200;
}
