using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
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
internal sealed class EmployeeCompanyDirectoryService(ITenantDbContextAccessor contextAccessor)
  : IEmployeeCompanyDirectory
{
  public async Task<Guid?> GetCompanyIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
  {
    if (employeeId == Guid.Empty)
    {
      return null;
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // No status predicate. A TERMINATED employee still has payslips and `REQ-SS-0006` requires them to stay
    // readable — filtering on active here would sever self-service at termination through the back door,
    // which is the mistake FP-015 records in four places.
    return await context.Set<Employee>().AsNoTracking()
      .Where(employee => employee.Id == employeeId)
      .Select(employee => (Guid?)employee.CompanyId)
      .SingleOrDefaultAsync(cancellationToken);
  }
}
