using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments.Reads;

// WHICH COMPANIES THE CALLER IS ASKING FOR (FP-007 Phase 2, ADR-025 decision 10).
//
// A CLOSED CHOICE, so "no company specified" is unrepresentable in the read contract.
public enum DepartmentCompanyScopeMode
{
  // The selected company, proven by the live validation before it is used.
  CurrentCompany = 0,

  // Every company currently authorized to this caller, materialized. Never the absence of a predicate.
  AllAuthorizedCompanies = 1
}

// The caller's INTENT. It carries no authority: the resolver turns it into a DepartmentReadScope only after
// proving the functional permission and the company dimension against live state, and refuses otherwise.
public sealed record DepartmentScopeRequest(
  DepartmentCompanyScopeMode CompanyScope = DepartmentCompanyScopeMode.CurrentCompany);

// THE ONLY WAY TO OBTAIN A DepartmentReadScope (FP-007 Phase 2).
public interface IDepartmentScopeResolver
{
  Task<Result<DepartmentReadScope>> ResolveAsync(
    DepartmentScopeRequest request, CancellationToken cancellationToken = default);

  // The WRITE-side equivalent: prove the functional permission and that this specific company is reachable.
  // Same two questions, asked of one company rather than a set, so a read path and a write path cannot
  // disagree about what "authorized" means.
  Task<Result> AuthorizeAsync(
    string permission, Guid companyId, CancellationToken cancellationToken = default);

  // Functional permission alone, for the point in a write where the company is not yet known — it comes
  // from the department being loaded. Company scope is then proven separately by AuthorizeAsync.
  Result RequirePermission(string permission);
}

// ---- IT CHECKS BOTH DIMENSIONS, INDEPENDENTLY, AND REFUSES IF EITHER FAILS.
//
// Functional permission and authorized company scope are separate questions with separate answers (ADR-025
// decision 8). They are asked in one place so that no path can forget one — not so that one can stand in
// for another. `Platform.Tenant.Administer` answers the SCOPE question by widening the company set and
// answers nothing about the PERMISSION question: an administrator without `HR.Departments.View` cannot read
// a department.
//
// ---- IT RESOLVES LIVE, EVERY TIME.
//
// Nothing is cached from login or from an earlier call in the same request. Company access, administrator
// authority and company status are all revocable inside a request's lifetime, and a read served from a
// stale set is precisely the failure this exists to prevent.
//
// ---- IT DELEGATES; IT DOES NOT RE-IMPLEMENT.
//
// The authorized set comes from `ITenantCompanyAccessResolver`, which is the single source of truth for
// that dimension and already intersects with active companies. A second opinion here is how a read path and
// a write path come to disagree about what a scope means.
//
// ---- THERE IS NO BRANCH DIMENSION, AND ITS ABSENCE IS DELIBERATE.
//
// A Department is not branch-owned, so branch scope does not determine whether one is VISIBLE. Employee
// membership remains branch-scoped by the Employee read path, which is untouched.
public sealed class DepartmentScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ICurrentCompany currentCompany,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IDepartmentScopeResolver
{
  public async Task<Result<DepartmentReadScope>> ResolveAsync(
    DepartmentScopeRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<DepartmentReadScope>(DepartmentErrors.InvalidActor);
    }

    // ---- DIMENSION 1: THE FUNCTIONAL PERMISSION. Asked FIRST and asked alone.
    var permitted = RequirePermission(HrPermissionNames.ViewDepartments);
    if (permitted.IsFailure)
    {
      return Result.Failure<DepartmentReadScope>(permitted.Error);
    }

    // ---- DIMENSION 2: THE AUTHORIZED COMPANY SET.
    var companies = await ResolveCompaniesAsync(request, tenantId, tenantUserId, cancellationToken);

    return companies.IsFailure
      ? Result.Failure<DepartmentReadScope>(companies.Error)
      : Result.Success(DepartmentReadScope.Create(tenantId, companies.Value));
  }

  public async Task<Result> AuthorizeAsync(
    string permission, Guid companyId, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(DepartmentErrors.InvalidActor);
    }

    var permitted = RequirePermission(permission);
    if (permitted.IsFailure)
    {
      return permitted;
    }

    // Re-asked against live state at the moment of the write, never answered from a set captured earlier in
    // the request. Access can be revoked and a company deactivated inside a session's lifetime.
    var authorized = await companyAccess.AuthorizeCompanyAsync(
      tenantId, tenantUserId, companyId, cancellationToken);

    return authorized.IsFailure ? Result.Failure(DepartmentErrors.CompanyScopeDenied) : Result.Success();
  }

  public Result RequirePermission(string permission) =>
    currentUser.Permissions.Contains(permission, StringComparer.Ordinal)
      ? Result.Success()
      : Result.Failure(DepartmentErrors.PermissionDenied);

  private async Task<Result<AuthorizedDepartmentCompanyScope>> ResolveCompaniesAsync(
    DepartmentScopeRequest request, Guid tenantId, long tenantUserId, CancellationToken cancellationToken)
  {
    if (request.CompanyScope == DepartmentCompanyScopeMode.AllAuthorizedCompanies)
    {
      var permitted = await companyAccess.GetPermittedCompaniesAsync(
        tenantId, tenantUserId, cancellationToken);
      if (permitted.IsFailure)
      {
        return Result.Failure<AuthorizedDepartmentCompanyScope>(DepartmentErrors.CompanyScopeDenied);
      }

      var companyIds = permitted.Value.Select(company => company.CompanyId).ToArray();

      // AN EMPTY AUTHORIZED SET REFUSES. It never degrades to unfiltered, and it never becomes an empty
      // predicate that a later reader might "optimise away".
      return companyIds.Length == 0
        ? Result.Failure<AuthorizedDepartmentCompanyScope>(DepartmentErrors.CompanyScopeDenied)
        : Result.Success(AuthorizedDepartmentCompanyScope.Create(companyIds));
    }

    // The selected company is INTENT until the resolver proves it: exists, belongs to this tenant, is
    // Active, and is reachable by this caller. It is re-asked here rather than trusted.
    if (currentCompany.CompanyId is not { } selected || selected == Guid.Empty)
    {
      return Result.Failure<AuthorizedDepartmentCompanyScope>(DepartmentErrors.CompanyScopeDenied);
    }

    var authorized = await companyAccess.AuthorizeCompanyAsync(
      tenantId, tenantUserId, selected, cancellationToken);

    return authorized.IsFailure
      ? Result.Failure<AuthorizedDepartmentCompanyScope>(DepartmentErrors.CompanyScopeDenied)
      : Result.Success(AuthorizedDepartmentCompanyScope.Create([selected]));
  }
}
