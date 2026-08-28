using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.HR.Contracts.Employment;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Application.Reads;

// A self-service read's subject and the scope derived from it. One object because they are resolved
// together and must not be able to disagree.
public sealed record OwnEmployeeReadScope(PayrollReadScope Scope, Guid EmployeeId);

// ==================================================================================================
// THE SELF-SERVICE READ SCOPE (FP-015, T-088) — A SEPARATE OBJECT, AND THE SEPARATION WAS MEASURED.
// ==================================================================================================
//
// ---- WHY NOT A METHOD ON `IPayrollScopeResolver`.
//
// It was, briefly. **Every Payroll command handler takes that resolver**, so adding two constructor
// dependencies to it made a self-service concern a construction-time dependency of every payroll write:
// twenty-five API tests failed DI validation with `CreatePayElementCommandHandler` unable to be built
// without an employee-company directory.
//
// **That is not a design opinion; it is what the container reported.** The module already argues the
// principle for `AuthorizeAsync` versus `ResolveAsync` — *"a write that filtered by a set would be asking
// a different question than the one it needs answered."* **Two questions, two objects.**
//
// ---- AND IT ASKS A DIFFERENT QUESTION FROM THE ADMINISTRATIVE ONE.
//
// `ResolveAsync` answers *which companies may this user ADMINISTER*, from their access grants. This asks
// *which employee am I* — and an employee's own payslips are theirs by virtue of the employee record, not
// by virtue of an administrative grant.
//
// **Without that substitution a plain employee holding the self permission and no `UserCompanyAccess` row
// would be refused `company.scope_denied`**, which would make the feature true only for employees an
// administrator had separately privileged. That is the half-wired shape FP-015 exists not to add to.
public interface IPayrollSelfServiceScopeResolver
{
  Task<Result<OwnEmployeeReadScope>> ResolveForOwnEmployeeAsync(
    string permissionName, CancellationToken cancellationToken = default);
}

public sealed class PayrollSelfServiceScopeResolver(
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser,
  IUserEmployeeResolver userEmployees,
  IEmployeeCompanyDirectory employeeCompanies) : IPayrollSelfServiceScopeResolver
{
  // FOUR STEPS, AND EACH REFUSAL MEANS SOMETHING DIFFERENT: a tenant session, the named permission, the
  // caller's employee, that employee's company. Steps three and four are what replace the company-access
  // lookup the administrative resolver performs.
  public async Task<Result<OwnEmployeeReadScope>> ResolveForOwnEmployeeAsync(
    string permissionName, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<OwnEmployeeReadScope>(PayrollScopeErrors.InvalidActor);
    }

    if (!currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal))
    {
      return Result.Failure<OwnEmployeeReadScope>(PayrollScopeErrors.ReadPermissionDenied);
    }

    var employeeId = await userEmployees.ResolveEmployeeIdAsync(tenantUserId, cancellationToken);
    if (employeeId is not { } employee)
    {
      return Result.Failure<OwnEmployeeReadScope>(PayrollErrors.NoLinkedEmployee);
    }

    // ---- A LINK OUTLIVING THE EMPLOYEE IT NAMES IS REACHABLE, AND IT ANSWERS THE SAME WAY.
    //
    // `ADR-030` Decision 4 makes a cross-database foreign key impossible, so nothing prevents it. **It
    // collapses into the same refusal deliberately:** the caller did nothing wrong, cannot act on the
    // difference, and distinguishing them would tell them a link exists pointing at a record that does not
    // — a `BR-PLT-0002` disclosure with extra steps.
    //
    // **The cost is named rather than hidden: a dangling link is invisible from the wire.** Detecting one
    // is a reconciliation concern belonging to whoever owns the link's lifecycle, not to a read.
    var companyId = await employeeCompanies.GetCompanyIdAsync(employee, cancellationToken);
    if (companyId is not { } company)
    {
      return Result.Failure<OwnEmployeeReadScope>(PayrollErrors.NoLinkedEmployee);
    }

    var scope = PayrollReadScope.Create(tenantId, [company]);

    return scope is null
      ? Result.Failure<OwnEmployeeReadScope>(PayrollScopeErrors.CompanyScopeDenied)
      : Result.Success(new OwnEmployeeReadScope(scope, employee));
  }
}
