using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Infrastructure.Persistence;

// ==================================================================================================
// THE PLACE WHERE THE DEPARTMENT SCOPE BECOMES SQL (FP-007 Phase 2, ADR-025 decision 10).
// ==================================================================================================
//
// ---- EVERY QUERY STATES BOTH DIMENSIONS, EXPLICITLY, IN ITS OWN PREDICATE.
//
// `TenantId = @tenant AND CompanyId IN (@companies)`, written out at every call site below. Neither is
// inherited: tenant has a global query filter and the predicate restates it anyway, so the query declares
// the invariant it depends on; company has no filter at all, deliberately and permanently, so the explicit
// predicate is the only thing scoping these reads.
//
// ---- TWO DIMENSIONS, AND THE THIRD IS ABSENT BY DESIGN.
//
// `EmployeeReadService` adds `BranchId IN (@branches)`. This does not, because a Department is not
// branch-owned: its VISIBILITY is a company question. A caller authorized for Riyadh only still reads the
// Finance department that spans Riyadh and Jeddah — what they must not see is who is in it, and employee
// membership stays behind the untouched Employee read path.
//
// ---- IT AUTHORIZES NOTHING.
//
// Holding a `DepartmentReadScope` IS the authorization, already performed against live state. Re-deciding
// it here would be a second opinion that could disagree with the one the write path uses.
internal sealed class DepartmentReadService(ITenantDbContextAccessor contextAccessor)
  : IDepartmentReadService
{
  public async Task<Result<DepartmentDetail>> GetAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The identifier is the LAST condition, not the first. A department outside the scope simply does not
    // match, so it is reported as absent rather than as forbidden — which is what stops this read being
    // used to probe for departments in companies the caller cannot see.
    var department = await Scoped(context, scope)
      .Where(item => item.Id == departmentId)
      .Select(item => new
      {
        item.Id,
        item.CompanyId,
        Code = item.Code.Value,
        Name = item.Name.Value,
        item.ParentDepartmentId,
        item.Status,
        item.RowVersion
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (department is null)
    {
      return Result.Failure<DepartmentDetail>(DepartmentErrors.NotFound);
    }

    // ---- THE MANAGER'S IDENTIFIER ONLY. NO JOIN TO THE EMPLOYEE SET, EVER.
    //
    // Joining here would hand out an employee's name and number on the strength of DEPARTMENT visibility,
    // and a department is company-visible while employees are branch-scoped. The handler resolves the
    // identifier through the employee read path, which applies that scope.
    var managerEmployeeId = await GetManagerEmployeeIdAsync(scope, departmentId, cancellationToken);

    return Result.Success(new DepartmentDetail(
      department.Id,
      department.CompanyId,
      department.Code,
      department.Name,
      department.ParentDepartmentId,
      department.Status,
      managerEmployeeId,
      // Filled in by the handler once the employee scope has had its say. Null here means "not resolved
      // yet"; whether there IS a manager is what ManagerEmployeeId above answers.
      Manager: null,
      department.RowVersion));
  }

  public async Task<Guid?> GetManagerEmployeeIdAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The association row carries its own TenantId, and it is stated again here rather than trusted from
    // the department that led us to it.
    var manager = await context.Set<DepartmentManager>()
      .AsNoTracking()
      .Where(item => item.TenantId == scope.TenantId && item.Id == departmentId)
      .Select(item => (Guid?)item.EmployeeId)
      .SingleOrDefaultAsync(cancellationToken);

    return manager;
  }

  public async Task<Result<PagedResult<DepartmentListItem>>> SearchAsync(
    DepartmentReadScope scope, SearchDepartmentsQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(query);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var filtered = Scoped(context, scope);

    if (query.Status is { } status)
    {
      filtered = filtered.Where(item => item.Status == status);
    }

    if (query.ParentDepartmentId is { } parentId)
    {
      filtered = filtered.Where(item => item.ParentDepartmentId == parentId);
    }

    if (!string.IsNullOrWhiteSpace(query.SearchText))
    {
      // Matched against the NORMALIZED code and the display name. The normalized column is binary-collated,
      // so the code half is an ordinal prefix match — which is why the caller's text is normalized the same
      // way the stored value was rather than compared raw.
      var text = query.SearchText.Trim();
      var normalized = text.ToUpperInvariant();

      filtered = filtered.Where(item =>
        item.NormalizedCode.StartsWith(normalized) || item.Name.Value.Contains(text));
    }

    var total = await filtered.CountAsync(cancellationToken);

    var page = new PageRequest(query.Page, query.PageSize);

    // ---- DETERMINISTIC ORDER, WITH A TIE-BREAK THAT CANNOT TIE.
    //
    // NormalizedCode is unique within a company, but a search may span several companies, so two rows can
    // share a code. Id breaks that remaining tie, which is what makes paging stable: without it, two pages
    // could return the same row or skip one entirely.
    var items = await filtered
      .OrderBy(item => item.NormalizedCode)
      .ThenBy(item => item.Id)
      .Skip(page.Skip)
      .Take(page.PageSize)
      .Select(item => new DepartmentListItem(
        item.Id,
        item.CompanyId,
        item.Code.Value,
        item.Name.Value,
        item.ParentDepartmentId,
        item.Status,
        item.RowVersion))
      .ToArrayAsync(cancellationToken);

    return Result.Success(
      new PagedResult<DepartmentListItem>(items, page.PageNumber, page.PageSize, total));
  }

  public async Task<Result<IReadOnlyList<DepartmentChild>>> GetChildrenAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The parent must itself be inside the scope. Without this, a caller could enumerate the children of a
    // department in a company they cannot see, and the children would be filtered while the fact that the
    // parent EXISTS had already leaked through a non-empty result.
    var parentVisible = await Scoped(context, scope)
      .AnyAsync(item => item.Id == departmentId, cancellationToken);

    if (!parentVisible)
    {
      return Result.Failure<IReadOnlyList<DepartmentChild>>(DepartmentErrors.NotFound);
    }

    var children = await Scoped(context, scope)
      .Where(item => item.ParentDepartmentId == departmentId)
      .OrderBy(item => item.NormalizedCode)
      .ThenBy(item => item.Id)
      .Select(item => new DepartmentChild(item.Id, item.Code.Value, item.Name.Value, item.Status))
      .ToArrayAsync(cancellationToken);

    return Result.Success<IReadOnlyList<DepartmentChild>>(children);
  }

  // THE ONE PLACE THE SCOPE PREDICATE IS WRITTEN. Every query above starts here, so none of them can start
  // anywhere else — and the scope sets are non-empty by construction, so the IN list is never empty.
  private static IQueryable<Department> Scoped(DbContext context, DepartmentReadScope scope) =>
    context.Set<Department>()
      .AsNoTracking()
      .Where(department =>
        department.TenantId == scope.TenantId &&
        scope.Companies.CompanyIds.Contains(department.CompanyId));
}
