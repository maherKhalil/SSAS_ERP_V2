using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Contracts.Employment;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// ================================================================================================
// THE THIRD SANCTIONED EMPLOYEE READ SHAPE (FP-013, OD-ATT-0007).
// ================================================================================================
//
// `EmployeeReadService` is the first and serves HR CALLERS: tenant + company + BRANCH.
// `EmployeeRosterService` is the second and serves PAYROLL: tenant + company, deliberately no branch
// (`DEC-PAY-0017`).
//
// This is the third, and it serves ATTENDANCE'S APPROVAL WALK. It gets its own structural shape rather than
// an exemption from either of the others, on the ruling that created the second one:
//
//   > A second door with a lock as good as the first door's is a sanctioned shape; a second door with a note
//   > saying "this one is fine" is an exception, and the difference is the whole point.
//
// The guard file listing the sanctioned shapes grows from two to three, with the reasoning inline.
//
// ---- WHY NO BRANCH PREDICATE HERE EITHER, STATED AT THE SITE.
//
// The approval chain runs through the DEPARTMENT tree, not the branch tree. `Department` is asserted NOT
// branch-owned, and a department manager legitimately manages people across branches — `Employee` carries
// both a `BranchId` and a `DepartmentId` precisely because they are SIBLING dimensions, not nested ones.
//
// A branch predicate would silently truncate the chain for any employee whose manager sits at another
// branch, and the failure would look like "no approver found" rather than like a bug.
//
// ---- WHAT IT RETURNS, AND WHAT IT REFUSES TO DECIDE.
//
// Three fields per candidate: employee, department, depth. HR walks the tree and applies HR's facts; the
// **self-approval bar is NOT applied here** — that is `BR-ATT-0007`, Attendance's rule, and filtering the
// requester out in this file would put the rule in the module that does not own it.
//
// ---- READ-ONLY, PERMANENTLY.
//
// Nothing here writes. `DEC-ATT-0003`: Attendance reads HR facts through a contract and never writes HR.
internal sealed class EmployeeApproverDirectoryService(
  ITenantDbContextAccessor contextAccessor,
  ITenantCompanyAccessResolver companyAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser) : IEmployeeApproverDirectory
{
  // ---- THE CYCLE BOUND, AND WHY IT IS NOT PARANOIA.
  //
  // `Department.ChangeParent` refuses self-parenting, but a LONGER cycle — A parents B, B parents A — is not
  // structurally impossible in the stored data. An approval walk is not the place to discover that by
  // hanging a request thread, so the walk is bounded and a chain longer than this simply stops.
  //
  // Fifty is far past any real org depth; it is a backstop, not a policy.
  private const int MaximumChainDepth = 50;

  public async Task<IReadOnlyList<ApproverCandidate>> GetApproverChainAsync(
    Guid companyId,
    Guid employeeId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    var authorized = await ResolveAuthorizedCompaniesAsync(cancellationToken);

    // Refusal is an exception, never an empty list — the `EmployeeRosterService` reasoning applies with more
    // force here, because an EMPTY LIST IS A MEANINGFUL ANSWER on this contract: it means "the chain is
    // exhausted, use the root fallback". Returning it for an authorization failure would route an
    // unauthorized caller into the permission-holder fallback path instead of refusing them.
    if (!authorized.Contains(companyId))
    {
      throw new UnauthorizedAccessException(
        "The caller has no authorized access to the requested company's approval chain.");
    }

    var tenantId = currentTenant.TenantId!.Value;

    var startingDepartmentId = await ApproverScoped(context, tenantId, companyId)
      .Where(employee => employee.Id == employeeId)
      .Select(employee => (Guid?)employee.DepartmentId)
      .FirstOrDefaultAsync(cancellationToken);

    if (startingDepartmentId is not { } departmentId || departmentId == Guid.Empty)
    {
      return [];
    }

    // ---- LOADED ONCE, WALKED IN MEMORY.
    //
    // The parent map and the manager seats are two small company-scoped sets. Walking the chain with one
    // query per level would issue up to fifty round trips for a single leave decision, and the tree is
    // shallow enough that loading it whole is cheaper than the first three of those.
    var parents = await context.Set<Department>()
      .AsNoTracking()
      .Where(department => department.TenantId == tenantId)
      .Where(department => department.CompanyId == companyId)
      .Select(department => new { department.Id, department.ParentDepartmentId })
      .ToDictionaryAsync(row => row.Id, row => row.ParentDepartmentId, cancellationToken);

    // ---- TERMINATED MANAGERS ARE EXCLUDED HERE, IN THE JOIN.
    //
    // `Department.ManagerTerminated` is a modelled error, so a terminated manager is a state HR contemplates
    // — and leave requests do not stop arriving because a manager left. Excluding them in the query rather
    // than after it means the chain naturally escalates to the parent, which is what `OD-ATT-0007` ruled.
    //
    // A department with no manager at all is simply absent from this dictionary, and absent candidates
    // cannot be accidentally selected the way a present-and-null one could.
    var seats = await context.Set<DepartmentManager>()
      .AsNoTracking()
      .Where(manager => manager.TenantId == tenantId)
      .Where(manager => manager.CompanyId == companyId)
      .Join(
        context.Set<Employee>()
          .AsNoTracking()
          .Where(employee => employee.TenantId == tenantId)
          .Where(employee => employee.Status != EmployeeStatus.Terminated),
        manager => manager.EmployeeId,
        employee => employee.Id,
        (manager, employee) => new { DepartmentId = manager.Id, ManagerEmployeeId = employee.Id })
      .ToDictionaryAsync(row => row.DepartmentId, row => row.ManagerEmployeeId, cancellationToken);

    var chain = new List<ApproverCandidate>();
    var visited = new HashSet<Guid>();
    Guid? current = departmentId;

    for (var depth = 0; current is { } node && depth < MaximumChainDepth; depth++)
    {
      if (!visited.Add(node))
      {
        // A cycle. Stop rather than loop; what has been collected so far is still a valid chain.
        break;
      }

      if (seats.TryGetValue(node, out var managerEmployeeId))
      {
        chain.Add(new ApproverCandidate(managerEmployeeId, node, depth));
      }

      current = parents.TryGetValue(node, out var parent) ? parent : null;
    }

    return chain;
  }

  // THE APPROVER WALK'S OWN SCOPED QUERY. Tenant and company, stated explicitly. No branch — see the header:
  // approval runs through departments, and branch is a sibling dimension that would truncate the chain.
  //
  // The tenant predicate is restated even though a global filter exists, for the reason HR's `Scoped` and
  // Payroll's `RosterScoped` both restate theirs: the query declares the invariant it depends on rather than
  // inheriting a configuration a future change could alter without touching this file.
  private static IQueryable<Employee> ApproverScoped(DbContext context, Guid tenantId, Guid companyId) =>
    context.Set<Employee>()
      .AsNoTracking()
      .Where(employee => employee.TenantId == tenantId)
      .Where(employee => employee.CompanyId == companyId);

  // Live, every call. Never cached, never accepted from a parameter — the property the scope types guarantee
  // is *checked live, just now*, and this read earns it by doing the work rather than by trusting a caller.
  private async Task<IReadOnlyList<Guid>> ResolveAuthorizedCompaniesAsync(CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      throw new UnauthorizedAccessException("The request does not carry a resolved tenant user.");
    }

    var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);

    // Fail closed, per `ITenantCompanyAccessResolver`'s own instruction: an empty answer is legitimate and
    // callers must not fall back to "all".
    return permitted.IsFailure
      ? []
      : permitted.Value.Select(company => company.CompanyId).ToArray();
  }
}
