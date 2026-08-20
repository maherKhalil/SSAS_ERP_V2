using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Departments;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Infrastructure.Persistence;

// Department persistence over the shared tenant ERP context (ADR-010, ADR-017).
//
// ---- WHAT IS AND IS NOT THIS REPOSITORY'S JOB.
//
// It tracks and reads. It does NOT authorize: the tenant and company boundaries live in the context's save
// pipeline and in the access resolvers, and a repository that re-checked them would be a second opinion
// that could disagree.
//
// Every query states TENANT and COMPANY explicitly where it has them. The tenant global query filter
// already restricts reads to the routed tenant, but the predicate says so anyway so it declares the
// invariant it depends on rather than inheriting it — and the COMPANY dimension has no global filter at all
// by deliberate design (ADR-025 decision 10), so stating it is the only thing scoping these reads.
//
// THERE IS NO DELETE. Physical Department deletion is prohibited, and the absence of a method here is the
// first of the two protections. The second is the RESTRICTED foreign keys from `DepartmentManagers` and
// `EmployeeDepartmentAssignments`, and from `Departments` to itself.
internal sealed class DepartmentRepository(ITenantDbContextAccessor contextAccessor) : IDepartmentRepository
{
  public async Task<Department?> GetByIdAsync(
    Guid departmentId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Tracked, not AsNoTracking: the caller is about to mutate it, and the change tracker carries the
    // original parent and status that later phases' guards compare against.
    return await context.Set<Department>()
      .SingleOrDefaultAsync(department => department.Id == departmentId, cancellationToken);
  }

  public async Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // COMPANY-SCOPED. The code is unique within the company, so a narrower predicate would report
    // "available" for a code already taken and leave the unique index to refuse the insert with a raw
    // persistence error.
    return await context.Set<Department>()
      .AsNoTracking()
      .AnyAsync(
        department => department.CompanyId == companyId && department.NormalizedCode == normalizedCode,
        cancellationToken);
  }

  public async Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedDepartmentId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Excluding the department itself, so renaming `SALES` to `SALES` is not a conflict with its own row.
    return await context.Set<Department>()
      .AsNoTracking()
      .AnyAsync(
        department => department.CompanyId == companyId &&
          department.NormalizedCode == normalizedCode &&
          department.Id != excludedDepartmentId,
        cancellationToken);
  }

  public async Task AddAsync(Department department, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(department);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<Department>().AddAsync(department, cancellationToken);
  }

  // ---- THE ANCESTRY WALK, ITERATIVE AND BOUNDED BY THE DATA (ADR-026 decision 4).
  //
  // One query per level rather than a recursive CTE. At department depth — single digits in practice — the
  // round trips are cheap, and the loop reads exactly the rows it needs from the change tracker or the
  // database in the caller's transaction. A recursive CTE would be faster on a deep tree and would return
  // detached projections the caller could not then mutate.
  //
  // ---- IT IS CYCLE-SAFE EVEN THOUGH WRITES PREVENT CYCLES.
  //
  // A `seen` set terminates the walk if the stored hierarchy ever DOES contain a cycle — from a direct SQL
  // write, a restore of corrupted data, or a defect in this very code. Without it such a row would hang the
  // request in an infinite loop, which is a far worse failure than the cycle itself. It is deliberately not
  // a depth cap: an arbitrary limit would refuse legitimate deep hierarchies, while `seen` only stops on an
  // actual repetition.
  public async Task<IReadOnlyList<Department>> GetAncestryAsync(
    Guid departmentId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var chain = new List<Department>();
    var seen = new HashSet<Guid>();
    Guid? currentId = departmentId;

    while (currentId is { } id && seen.Add(id))
    {
      var current = await context.Set<Department>()
        .SingleOrDefaultAsync(department => department.Id == id, cancellationToken);

      if (current is null)
      {
        break;
      }

      chain.Add(current);
      currentId = current.ParentDepartmentId;
    }

    return chain;
  }

  public async Task<bool> HasActiveChildrenAsync(
    Guid departmentId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<Department>()
      .AsNoTracking()
      .AnyAsync(
        department => department.ParentDepartmentId == departmentId &&
          department.Status == DepartmentStatus.Active,
        cancellationToken);
  }

  public async Task<DepartmentManager?> GetManagerAsync(
    Guid departmentId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<DepartmentManager>()
      .SingleOrDefaultAsync(manager => manager.Id == departmentId, cancellationToken);
  }

  public async Task SetManagerAsync(
    DepartmentManager manager, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(manager);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<DepartmentManager>().AddAsync(manager, cancellationToken);
  }

  public async Task ClearManagerAsync(
    DepartmentManager manager, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(manager);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- THE ONE REMOVE IN THIS REPOSITORY, AND IT IS NOT A DEPARTMENT DELETE.
    //
    // It removes an ASSOCIATION. "This department has no manager" is the absence of the row, and the
    // no-physical-delete rule governs departments, not the record of who currently heads one. If manager
    // history is ever required it will be a separate append-only log, exactly as branch history is — at
    // which point this method disappears rather than being reinterpreted.
    context.Set<DepartmentManager>().Remove(manager);
  }

  public async Task AppendDepartmentAssignmentAsync(
    EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(assignment);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // APPEND ONLY. There is no update and no remove counterpart, here or anywhere: a correction is another
    // department change, never a rewrite.
    await context.Set<EmployeeDepartmentAssignment>().AddAsync(assignment, cancellationToken);
  }
}
