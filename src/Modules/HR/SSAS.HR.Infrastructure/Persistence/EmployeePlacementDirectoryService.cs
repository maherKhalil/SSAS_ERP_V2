using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.HR.Contracts.Employment;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// HR's side of FP-015's self-service read (T-088). The FOURTH sanctioned employee read shape.
//
// ---- IT TAKES NO COMPANY AND APPLIES NO COMPANY AUTHORIZATION, UNLIKE ITS THREE SIBLINGS.
//
// `EmployeeRosterService` and `EmployeeApproverDirectoryService` both resolve the caller's authorized
// companies and throw when the requested one is absent. **This one must not**, and the reason is the whole
// point of the contract: the caller is an ordinary employee reading their own record, and an employee is
// not necessarily granted access to administer the company they work for.
//
// **Applying the same check here would refuse exactly the caller this exists for**, and would reintroduce
// the dependency FP-015 removed.
//
// ---- WHAT STILL ISOLATES, STATED BECAUSE SOMETHING WAS REMOVED.
//
// The tenant database's global tenant filter. An employee identifier belonging to another tenant is not
// found, and the identifier itself arrives from `UserEmployeeLink`, which is keyed by tenant and
// tenant-user. **Nothing widens: the caller can ask about exactly one employee, and only because a
// Platform-side link says it is theirs.**
//
// Nothing here writes, following `DEC-ATT-0003`'s rule for the sibling contracts.
//
// ---- IT ALSO ANSWERS `IEmploymentStandingDirectory` (T-090), AND THAT IS ONE FILE ON PURPOSE.
//
// The employee-set door list is EXACT — `Only_the_read_service_and_the_write_repository_reach_the_employee_set`
// — and a fifth file touching `Set<Employee>()` would be a fifth read shape to review. **This is not a new
// shape.** Same lock, stated in the same terms: tenant isolation, plus an identifier that is never
// caller-supplied. Putting it here opens no door.
//
// The two contracts stay SEPARATE INTERFACES even so, because their callers are different and their
// injection sets are pinned separately: the placement directory is for the two self-service scope
// resolvers, the standing directory is for the Platform seam and nothing else.
internal sealed class EmployeePlacementDirectoryService(ITenantDbContextAccessor contextAccessor)
  : IEmployeePlacementDirectory, IEmploymentStandingDirectory
{
  public async Task<EmployeePlacement?> GetPlacementAsync(
    Guid employeeId, CancellationToken cancellationToken = default)
  {
    if (employeeId == Guid.Empty)
    {
      return null;
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- STILL NO STATUS PREDICATE, AND THE REASON CHANGED IN T-090 (`DEC-L-073`).
    //
    // It used to read: *filtering on active here would sever self-service at termination through the back
    // door.* **`AC-SS-0012` then ruled that self-service MUST close at termination** — so the outcome that
    // sentence warned against is now the intended one, reached at a different seam.
    //
    // The predicate still does not belong here, for a reason that survives the ruling: **ONE place decides
    // whether a terminated employee resolves, and it is `IUserEmployeeResolver`.** A second check here
    // would be the per-handler shape `REQ-SS-0003` rejected, and the first time the two disagreed nobody
    // would know which one was authoritative.
    //
    // `REQ-SS-0006` is untouched by either: the LINK survives termination, so retained payslips stay
    // attributable. What closes is the resolution, not the record.
    // Both dimensions from one row, in one query. Two queries would be two chances for the branch to be
    // the one nobody fetched.
    return await context.Set<Employee>().AsNoTracking()
      .Where(employee => employee.Id == employeeId)
      .Select(employee => new EmployeePlacement(employee.CompanyId, employee.BranchId))
      .SingleOrDefaultAsync(cancellationToken);
  }

  // ---- THE STANDING READ (T-090). PROJECTS THE STATUS AND NOTHING ELSE.
  //
  // It returns a THREE-VALUED standing rather than the `EmployeeStatus` itself, because `EmployeeStatus` is
  // an HR domain type and this contract is consumed by Platform. Mapping here keeps HR's enum inside HR: a
  // fourth status added later is a change to this switch, not to a Platform file.
  //
  // `Inactive` maps to `Current` deliberately — unpaid leave and suspension leave the employment
  // relationship intact and are fully reversible, and refusing self-service to someone on unpaid leave
  // would be a rule nobody asked for.
  public async Task<EmploymentStanding> GetStandingAsync(
    Guid employeeId, CancellationToken cancellationToken = default)
  {
    if (employeeId == Guid.Empty)
    {
      return EmploymentStanding.Unknown;
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Nullable so "no such employee" is distinguishable HERE from a status value, and collapses to
    // `Unknown` on the way out. The tenant filter is what makes another tenant's employee invisible.
    var status = await context.Set<Employee>().AsNoTracking()
      .Where(employee => employee.Id == employeeId)
      .Select(employee => (EmployeeStatus?)employee.Status)
      .SingleOrDefaultAsync(cancellationToken);

    return status switch
    {
      EmployeeStatus.Active or EmployeeStatus.Inactive => EmploymentStanding.Current,
      EmployeeStatus.Terminated => EmploymentStanding.Ended,
      _ => EmploymentStanding.Unknown
    };
  }
}
