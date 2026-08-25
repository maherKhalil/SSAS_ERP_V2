using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Contracts.Employment;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// ================================================================================================
// THE SECOND SANCTIONED EMPLOYEE READ SHAPE (RULED 2026-08-24, DEC-PAY-0017).
// ================================================================================================
//
// `EmployeeReadService` is the first, and it serves HR CALLERS: tenant + company + BRANCH, every predicate
// from a proven `EmployeeReadScope`. Its architecture guard demanded exactly one query site and all three
// predicates, and when FP-012's roster was first written it failed that guard **twice, correctly**.
//
// The ruling that followed is the important part, and it is why this file exists rather than an exemption:
//
//   > **The branch predicate never protected "employees" in the abstract; it protects HR CALLERS from
//   > exceeding their branch authority.** A cross-module roster read is a different regime with its own
//   > authority — the payroll permission plus company access — and it gets its own STRUCTURAL SHAPE, not an
//   > exemption from HR's.
//
// So: two sanctioned read shapes now exist, each with its own invariants, **each with its own guard**. A
// second door with a lock as good as the first door's is a sanctioned shape; a second door with a note
// saying "this one is fine" is an exception, and the difference is the whole point.
//
// ---- WHY THERE IS NO BRANCH PREDICATE HERE, STATED AT THE SITE.
//
// **Payroll pays the company.** Runs are company-owned (`OD-PAY-0005`), periods are company-scoped
// (`OD-PAY-0002`), and `OD-GL-0005` already established that finance is not branch-dimensional. A
// branch-scoped roster would mean payroll ran per branch, which contradicts all three. Branch authority is
// an HR-caller concern, and this is not an HR caller.
//
// ---- WHY THE COMPANY SET IS RESOLVED HERE AND NEVER ACCEPTED.
//
// This method takes a `companyId` and then **resolves the caller's permitted companies LIVE**, inside this
// implementation, from `ITenantCompanyAccessResolver`. It does not accept an `AuthorizedCompanySet`
// parameter, and that is deliberate: a set handed in would be forgeable by whoever called, and the whole
// property the scope types guarantee is *checked live, just now*. This read earns the same property by
// doing the same work rather than by trusting a caller who claims to have done it.
//
// Payroll authorizes the company through its OWN resolver before calling. This is not redundant — it is the
// second of two independent checks, in the module that owns the data.
//
// ---- READ-ONLY, PERMANENTLY (DEC-PAY-0014).
//
// Nothing here writes, and the ruling that created this file opened no write path. A method that mutated an
// employee from a payroll caller would silently reverse `DEC-POS-0023` by putting compensation into HR
// through a side door.
internal sealed class EmployeeRosterService(
  ITenantDbContextAccessor contextAccessor,
  ITenantCompanyAccessResolver companyAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser) : IEmployeeRoster
{
  public async Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
    Guid companyId,
    DateTimeOffset fromUtc,
    DateTimeOffset toUtc,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    var authorized = await ResolveAuthorizedCompaniesAsync(cancellationToken);

    // ---- REFUSAL IS AN EXCEPTION HERE, NOT AN EMPTY LIST, AND THAT IS DELIBERATE.
    //
    // An empty list would claim "this company employs nobody", which is a statement about the DATA. A
    // payroll run built on it would calculate cleanly, produce no lines, and refuse at approval with
    // "no included employees" — a message that sends someone hunting through HR for employees that exist.
    //
    // Reaching here unauthorized also is not a business outcome: Payroll authorizes the company through its
    // own resolver first, so a disagreement between the two means a defect, not a user who lacks a grant.
    if (!authorized.Contains(companyId))
    {
      throw new UnauthorizedAccessException(
        "The caller has no authorized access to the requested company's employment roster.");
    }

    var from = fromUtc.ToUniversalTime();
    var to = toUtc.ToUniversalTime();

    // OVERLAP, not membership: employment began on or before the window ends and had not already ended
    // before it began — the widest honest candidate set.
    //
    // **Who actually gets paid is not decided here.** `PayrollPeriod.Includes` carries the `BR-HR-0004`
    // reading `OD-PAY-0010` ruled, and it stays in the module that owns the ruling. If HR filtered too, the
    // same rule would live in two modules, with the enforcing one being the module that never agreed to it.
    //
    // ---- THE PROJECTION IS THE CONTRACT'S SHAPE AND NOTHING MORE.
    //
    // Four fields. **No `NationalId`, no `FullName`, no department, position, branch or status reason.** The
    // most-sensitive-field discipline travels WITH the data across a module boundary, and a contract is
    // forever-ish: a roster returning `EmployeeDetail` would let every future Payroll feature read HR
    // personal data with no call-site change for anyone to review. A guard asserts this field list exactly.
    return await RosterScoped(context, currentTenant.TenantId!.Value, companyId)
      .Where(employee =>
        employee.EmploymentDate <= to &&
        (employee.TerminationDate == null || employee.TerminationDate >= from))
      .OrderBy(employee => employee.Id)
      .Select(employee => new EmploymentRecord(
        employee.Id,
        employee.CompanyId,
        employee.EmploymentDate,
        employee.TerminationDate))
      .ToListAsync(cancellationToken);
  }

  // THE ROSTER'S OWN SCOPED QUERY. Tenant and company, stated explicitly, exactly as HR's `Scoped` states its
  // three. The tenant predicate is restated even though a global filter exists, for the same reason HR
  // restates it: the query declares the invariant it depends on rather than inheriting a configuration a
  // future change could alter without touching this file.
  //
  // There is deliberately no overload without a company, and no way to call it with a company the caller has
  // not been proven to hold — `GetEmploymentAsync` resolves that before this runs.
  private static IQueryable<Employee> RosterScoped(DbContext context, Guid tenantId, Guid companyId) =>
    context.Set<Employee>()
      .AsNoTracking()
      .Where(employee => employee.TenantId == tenantId)
      .Where(employee => employee.CompanyId == companyId);

  // Live, every call. Never cached, never accepted from a parameter.
  private async Task<IReadOnlyList<Guid>> ResolveAuthorizedCompaniesAsync(CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      throw new UnauthorizedAccessException("The request does not carry a resolved tenant user.");
    }

    var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);

    // Fail closed. `ITenantCompanyAccessResolver` documents an empty answer as legitimate and instructs
    // callers to fail closed rather than fall back to "all" — this does exactly that.
    return permitted.IsFailure
      ? []
      : permitted.Value.Select(company => company.CompanyId).ToArray();
  }
}
