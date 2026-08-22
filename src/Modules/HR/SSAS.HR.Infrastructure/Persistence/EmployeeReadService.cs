using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// ==================================================================================================
// THE PLACE WHERE THE SCOPE BECOMES SQL (FP-006C4, ADR-023 decision 22, ADR-025 decision 10).
// ==================================================================================================
//
// ---- EVERY QUERY STATES ALL THREE DIMENSIONS, EXPLICITLY, IN ITS OWN PREDICATE.
//
// TenantId = @tenant AND CompanyId IN (@companies) AND BranchId IN (@branches), written out at every call
// site below. Not one of them is inherited:
//
//   * TENANT has a global query filter, and the predicate says so anyway. The filter is the enforcement;
//     restating it means the query declares the invariant it depends on rather than depending on a
//     configuration a future change could alter without touching this file.
//   * COMPANY and BRANCH have NO global filter, deliberately and permanently. A filter reads a single
//     ambient value, so it cannot express "these three branches" — the multi-branch scope modes are
//     inexpressible as a filter. It is invisible at the call site, so an author cannot see whether a query
//     is scoped. And IgnoreQueryFilters() silently removes it, turning a scoped read into a tenant-wide one
//     with a single method call and no compiler complaint. Explicit predicates have none of those
//     properties.
//
// ---- "ALL" IS ALWAYS A LIST OF IDENTIFIERS, NEVER A MISSING CONDITION.
//
// AllAuthorizedBranches and AllAuthorizedCompanies arrive here already materialized by the resolver, so the
// SQL is an IN list in every mode. There is no code path that omits a scope predicate, which is why the
// scope sets are non-empty by construction: an empty IN list cannot be reached.
//
// ---- IT AUTHORIZES NOTHING.
//
// Holding an EmployeeReadScope IS the authorization, already performed against live state. Re-deciding it
// here would be a second opinion that could disagree with the one the write path uses.
internal sealed class EmployeeReadService(ITenantDbContextAccessor contextAccessor) : IEmployeeReadService
{
  // Both current-employment states. The default when a caller names no status: Terminated is excluded from
  // routine reads unless asked for by name (api-contracts).
  private static readonly EmployeeStatus[] DefaultStatuses =
    [EmployeeStatus.Active, EmployeeStatus.Inactive];

  public async Task<EmployeeDetail?> GetEmployeeAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The identifier is the LAST condition, not the first. An employee outside the scope simply does not
    // match, so it is reported as absent rather than as forbidden.
    return await Scoped(context, scope)
      .Where(employee => employee.Id == employeeId)
      .Select(employee => new EmployeeDetail(
        employee.Id,
        employee.CompanyId,
        employee.BranchId,
        employee.DepartmentId,
        employee.EmployeeNumber.Value,
        employee.FullName.Value,
        employee.NationalId == null ? null : employee.NationalId.Value,
        employee.EmploymentDate,
        employee.TerminationDate,
        employee.Status,
        employee.StatusChangeReasonCode,
        employee.StatusChangedUtc,
        employee.RowVersion))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<PagedResult<EmployeeSummary>> SearchEmployeesAsync(
    EmployeeReadScope scope, EmployeeSearchCriteria criteria, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(criteria);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    var query = Scoped(context, scope);

    var statuses = criteria.Statuses is { Count: > 0 }
      ? criteria.Statuses.Distinct().ToArray()
      : DefaultStatuses;

    query = query.Where(employee => statuses.Contains(employee.Status));

    if (!string.IsNullOrWhiteSpace(criteria.EmployeeNumber))
    {
      // EXACT MATCH ON THE NORMALIZED COLUMN, normalized here the same way the domain normalizes it on
      // write. Comparing against the display column instead would be both case-sensitive under the binary
      // collation and inconsistent with the unique index that decides what "the same number" means.
      var normalized = criteria.EmployeeNumber.Trim().ToUpperInvariant();

      query = query.Where(employee => employee.NormalizedEmployeeNumber == normalized);
    }

    // ---- THE DEPARTMENT FILTER IS APPLIED **ON TOP OF** THE SCOPE, NEVER INSTEAD OF IT.
    //
    // `query` already carries the tenant, company and branch predicates from Scoped() above, and this only
    // ever adds a conjunct. There is no branch of code where naming a department replaces or relaxes them,
    // which is what makes the branch-visibility proof in FP-007 Phase 3 §25 hold structurally rather than
    // by inspection.
    if (criteria.DepartmentId is { } departmentId && departmentId != Guid.Empty)
    {
      query = query.Where(employee => employee.DepartmentId == departmentId);
    }

    // COUNTED THROUGH THE SAME SCOPED QUERY. A total computed from a wider query would leak the size of the
    // data outside the caller's scope even though none of those rows were returned.
    var totalCount = await query.CountAsync(cancellationToken);

    // ---- TOTALLY ORDERED, so paging is stable.
    //
    // FullName alone is not unique, and an unstable sort silently drops and duplicates rows across page
    // boundaries. EmployeeId is the tiebreaker (api-contracts).
    var items = await query
      .OrderBy(employee => employee.FullName)
      .ThenBy(employee => employee.Id)
      .Skip((criteria.PageNumber - 1) * criteria.PageSize)
      .Take(criteria.PageSize)
      .Select(employee => new EmployeeSummary(
        employee.Id,
        employee.CompanyId,
        employee.BranchId,
        employee.DepartmentId,
        employee.EmployeeNumber.Value,
        employee.FullName.Value,
        employee.EmploymentDate,
        employee.Status))
      .ToArrayAsync(cancellationToken);

    return new PagedResult<EmployeeSummary>(items, criteria.PageNumber, criteria.PageSize, totalCount);
  }

  public async Task<IReadOnlyList<EmployeeBranchHistoryEntry>?> GetEmployeeBranchHistoryAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- STEP 1: PROVE THE EMPLOYEE IS INSIDE THE SCOPE.
    //
    // This is the ONLY thing standing between a caller confined to one branch and the full list of every
    // branch an arbitrary employee has ever worked in. The assignment rows are company-owned but NOT
    // branch-owned — each names a source and a destination and belongs to neither — so no branch predicate
    // can be written over them, and their scope has to be inherited from the employee they describe.
    var employeeInScope = await Scoped(context, scope)
      .AnyAsync(employee => employee.Id == employeeId, cancellationToken);

    if (!employeeInScope)
    {
      // Absent, not forbidden — the same answer GetEmployeeAsync gives, so the two cannot be compared to
      // learn that the employee exists somewhere else.
      return null;
    }

    // ---- STEP 2: ONLY NOW LOAD THE HISTORY.
    //
    // Still tenant- and company-scoped in its own right. The employee predicate above already implies the
    // company, and stating it again means a future change to either query cannot silently widen this one.
    return await context.Set<EmployeeBranchAssignment>()
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == scope.TenantId)
      .Where(assignment => scope.Companies.CompanyIds.Contains(assignment.CompanyId))
      .Where(assignment => assignment.EmployeeId == employeeId)
      // ---- THE POINT-IN-TIME PRIMITIVE.
      //
      // Ordered by EffectiveFromUtc then AssignmentId, so "where was this employee at time T" is the LAST
      // row with EffectiveFromUtc at or before T — a total order the caller can rely on rather than one
      // that happens to hold. The identifier tiebreaker matters because two assignments can share an
      // instant.
      .OrderBy(assignment => assignment.EffectiveFromUtc)
      .ThenBy(assignment => assignment.Id)
      .Select(assignment => new EmployeeBranchHistoryEntry(
        assignment.Id,
        assignment.SourceBranchId,
        assignment.DestinationBranchId,
        assignment.EffectiveFromUtc,
        assignment.ReasonCode,
        assignment.ReasonText,
        assignment.TransferredBy))
      .ToArrayAsync(cancellationToken);
  }

  // ---- THE ONE PLACE THE THREE PREDICATES ARE WRITTEN.
  //
  // Every employee read starts here, so no read can be authored that forgets a dimension, and the shape of
  // the composed predicate is auditable in a single method. AsNoTracking because a read must never hand a
  // caller an entity whose navigations would load rows outside the scope on access. The deferred query type
  // stays inside this class and never crosses the application boundary.
  // ---- THE POSITION HISTORY (FP-008 Phase 4, FR-POS-0212).
  //
  // The branch history's method, step for step, because the control is the same one: the assignment rows are
  // company-owned but NOT branch-owned, so no branch predicate can be written over them and their scope has
  // to be inherited from the employee they describe.
  public async Task<IReadOnlyList<EmployeePositionHistoryEntry>?> GetEmployeePositionHistoryAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- STEP 1: PROVE THE EMPLOYEE IS INSIDE THE SCOPE.
    //
    // This is the only thing standing between a caller confined to one branch and the full promotion history
    // of an arbitrary employee. Absent, not forbidden — the same answer `GetEmployeeAsync` gives, so the two
    // cannot be compared to learn that the employee exists somewhere else.
    var employeeInScope = await Scoped(context, scope)
      .AnyAsync(employee => employee.Id == employeeId, cancellationToken);

    if (!employeeInScope)
    {
      return null;
    }

    // ---- STEP 2: ONLY NOW LOAD THE HISTORY.
    //
    // Still tenant- and company-scoped in its own right. The employee predicate above already implies the
    // company, and stating it again means a future change to either query cannot silently widen this one.
    return await context.Set<SSAS.HR.Domain.Positions.EmployeePositionAssignment>()
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == scope.TenantId)
      .Where(assignment => scope.Companies.CompanyIds.Contains(assignment.CompanyId))
      .Where(assignment => assignment.EmployeeId == employeeId)
      // The same point-in-time primitive: ordered by EffectiveFromUtc then identifier, so "which position did
      // this employee hold at time T" is the LAST row at or before T — a total order rather than one that
      // happens to hold. The tiebreaker matters because two changes can share an instant.
      .OrderBy(assignment => assignment.EffectiveFromUtc)
      .ThenBy(assignment => assignment.Id)
      .Select(assignment => new EmployeePositionHistoryEntry(
        assignment.Id,
        assignment.SourcePositionId,
        assignment.DestinationPositionId,
        assignment.EffectiveFromUtc,
        assignment.ReasonCode,
        assignment.ReasonText,
        assignment.ChangedBy))
      .ToArrayAsync(cancellationToken);
  }

  // Counted THROUGH `Scoped`, so the caller's company and branch predicates apply to the count exactly as
  // they apply to a list. That is what makes the number scope-dependent by construction rather than by a
  // filter someone remembered to add.
  public async Task<int> CountEmployeesByPositionAsync(
    EmployeeReadScope scope, Guid positionId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await Scoped(context, scope)
      .CountAsync(employee => employee.PositionId == positionId, cancellationToken);
  }

  // The department sibling, counted through the same `Scoped` predicate for the same reason: the number is
  // scope-dependent BY CONSTRUCTION rather than by a filter someone remembered to add.
  //
  // Note what is NOT filtered here, matching the position counter exactly: employment STATUS. A terminated
  // employee still carries the department they were in, and this counts them. That is the shipped position
  // behaviour, and the two counts must not disagree about what "an employee" means — if a status-aware
  // headcount is wanted later it is a different field with a different name, not a quiet change here.
  public async Task<int> CountEmployeesByDepartmentAsync(
    EmployeeReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await Scoped(context, scope)
      .CountAsync(employee => employee.DepartmentId == departmentId, cancellationToken);
  }

  private static IQueryable<Employee> Scoped(DbContext context, EmployeeReadScope scope) =>
    context.Set<Employee>()
      .AsNoTracking()
      .Where(employee => employee.TenantId == scope.TenantId)
      .Where(employee => scope.Companies.CompanyIds.Contains(employee.CompanyId))
      .Where(employee => scope.Branches.BranchIds.Contains(employee.BranchId));
}
