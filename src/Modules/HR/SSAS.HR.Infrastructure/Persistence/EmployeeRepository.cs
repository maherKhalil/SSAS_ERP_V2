using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// Employee persistence over the shared tenant ERP context (ADR-010, ADR-017).
//
// ---- WHAT IS AND IS NOT THIS REPOSITORY'S JOB.
//
// It tracks and reads. It does NOT authorize: the tenant, company and branch boundaries live in the context's
// save pipeline and in the access resolvers, and a repository that re-checked them would be a second opinion
// that could disagree.
//
// Every query states TENANT and COMPANY explicitly. The tenant global query filter already restricts reads
// to the routed tenant, but the predicate says so anyway so it declares the invariant it depends on rather
// than inheriting it — and the COMPANY dimension has no global filter at all by deliberate design (ADR-025
// decision 10), so stating it is the only thing scoping these reads.
//
// THERE IS NO DELETE. Physical Employee deletion is prohibited, and the absence of a method here is the
// first of the two protections. The second is the RESTRICTED foreign key from EmployeeBranchAssignments,
// which is itself append-only: an Employee always has at least its initial assignment, so the database
// refuses the delete and the history that would have to go with it cannot be removed either.
internal sealed class EmployeeRepository(ITenantDbContextAccessor contextAccessor) : IEmployeeRepository
{
  public async Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Tracked, not AsNoTracking: the caller is about to mutate it, and the change tracker is what carries
    // the original BranchId the sanctioned transfer channel matches against.
    //
    // The branch history is loaded with it so an append lands on a fully-known aggregate rather than one
    // whose collection would silently look empty.
    return await context.Set<Employee>()
      .Include(employee => employee.BranchAssignments)
      .SingleOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
  }

  public async Task<bool> EmployeeNumberExistsAsync(
    Guid companyId, string normalizedEmployeeNumber, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // COMPANY-SCOPED, AND DELIBERATELY NOT BRANCH-SCOPED. BR-HR-0001 makes the number unique within the
    // company, so a branch predicate here would report "available" for a number already taken in a sibling
    // branch — and the unique index would then refuse the insert with a raw persistence error.
    return await context.Set<Employee>()
      .AsNoTracking()
      .AnyAsync(
        employee => employee.CompanyId == companyId &&
          employee.NormalizedEmployeeNumber == normalizedEmployeeNumber,
        cancellationToken);
  }

  public async Task<bool> NationalIdExistsAsync(
    Guid companyId, string normalizedNationalId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<Employee>()
      .AsNoTracking()
      .AnyAsync(
        employee => employee.CompanyId == companyId &&
          employee.NormalizedNationalId == normalizedNationalId,
        cancellationToken);
  }

  public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(employee);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The initial branch-assignment record is reachable through the aggregate's collection, so EF adds both
    // in this one call and commits them in one transaction. An Employee cannot be persisted without its
    // history because there is no path that adds only one of them.
    await context.Set<Employee>().AddAsync(employee, cancellationToken);
  }

  public async Task AppendBranchAssignmentAsync(
    EmployeeBranchAssignment assignment, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(assignment);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // APPEND ONLY. There is no update and no remove counterpart, here or anywhere: a correction is another
    // transfer, never a rewrite.
    await context.Set<EmployeeBranchAssignment>().AddAsync(assignment, cancellationToken);
  }
}
