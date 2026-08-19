using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// THE ONLY WAY TO OBTAIN AN EmployeeReadScope (FP-006C4, ADR-023 d22, ADR-025 d10).
public interface IEmployeeScopeResolver
{
  Task<Result<EmployeeReadScope>> ResolveAsync(
    EmployeeScopeRequest request, CancellationToken cancellationToken = default);
}

// ---- IT CHECKS ALL THREE DIMENSIONS, INDEPENDENTLY, AND REFUSES IF ANY ONE FAILS.
//
// Functional permission, authorized company scope and authorized branch scope are separate questions with
// separate answers (`ADR-025` decision 8). They are asked in one place so that no read path can forget one
// — not so that one can stand in for another. Each has its own refusal below, and holding
// `Platform.Tenant.Administer` answers the two SCOPE questions while answering nothing about the
// PERMISSION question.
//
// ---- IT RESOLVES LIVE, EVERY TIME.
//
// Nothing is cached from login or from an earlier call in the same request. Company access, branch access,
// administrator authority, company status and branch status are all revocable inside a request's lifetime,
// and a read served from a stale set is precisely the failure this exists to prevent.
//
// ---- IT DELEGATES; IT DOES NOT RE-IMPLEMENT.
//
// The authorized sets come from ITenantCompanyAccessResolver and ITenantBranchAccessResolver, which are the
// single sources of truth for their dimensions and already intersect with active companies and branches. A
// second opinion here is how a read path and a write path come to disagree about what a scope means.
public sealed class EmployeeScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ITenantBranchAccessResolver branchAccess,
  ICurrentBranchResolver currentBranch,
  ICurrentCompany currentCompany,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IEmployeeScopeResolver
{
  public async Task<Result<EmployeeReadScope>> ResolveAsync(
    EmployeeScopeRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<EmployeeReadScope>(EmployeeErrors.InvalidActor);
    }

    // ---- DIMENSION 1: THE FUNCTIONAL PERMISSION.
    //
    // Asked FIRST and asked alone. Platform.Tenant.Administer widens the two scope dimensions below and
    // grants nothing here: an administrator without HR.Employees.View cannot read an employee, which is the
    // independence ADR-025 decision 8 exists to protect.
    if (!currentUser.Permissions.Contains(HrPermissionNames.ViewEmployees, StringComparer.Ordinal))
    {
      return Result.Failure<EmployeeReadScope>(EmployeeErrors.ReadPermissionDenied);
    }

    // ---- DIMENSION 2: THE AUTHORIZED COMPANY SET.
    var companies = await ResolveCompaniesAsync(request, tenantId, tenantUserId, cancellationToken);
    if (companies.IsFailure)
    {
      return Result.Failure<EmployeeReadScope>(companies.Error);
    }

    // ---- DIMENSION 3: THE AUTHORIZED BRANCH SET.
    var branches = await ResolveBranchesAsync(request, tenantId, tenantUserId, cancellationToken);
    if (branches.IsFailure)
    {
      return Result.Failure<EmployeeReadScope>(branches.Error);
    }

    return Result.Success(EmployeeReadScope.Create(tenantId, companies.Value, branches.Value));
  }

  private async Task<Result<AuthorizedCompanyScope>> ResolveCompaniesAsync(
    EmployeeScopeRequest request, Guid tenantId, long tenantUserId, CancellationToken cancellationToken)
  {
    if (request.CompanyScope == EmployeeCompanyScopeMode.AllAuthorizedCompanies)
    {
      var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
      if (permitted.IsFailure)
      {
        return Result.Failure<AuthorizedCompanyScope>(EmployeeErrors.CompanyScopeDenied);
      }

      var companyIds = permitted.Value.Select(company => company.CompanyId).ToArray();

      // AN EMPTY AUTHORIZED SET REFUSES. It never degrades to unfiltered, and it never becomes an empty
      // predicate that a later reader might "optimise away".
      return companyIds.Length == 0
        ? Result.Failure<AuthorizedCompanyScope>(EmployeeErrors.CompanyScopeDenied)
        : Result.Success(AuthorizedCompanyScope.Create(companyIds));
    }

    // ---- CurrentCompany. The selected company is INTENT until the resolver proves it: exists, belongs to
    // this tenant, is Active, and is reachable by this caller. ICurrentCompany only ever reports a value
    // that already passed that validation, and it is re-asked here rather than trusted.
    if (currentCompany.CompanyId is not { } selected || selected == Guid.Empty)
    {
      return Result.Failure<AuthorizedCompanyScope>(EmployeeErrors.CompanyScopeDenied);
    }

    var authorized = await companyAccess.AuthorizeCompanyAsync(
      tenantId, tenantUserId, selected, cancellationToken);

    return authorized.IsFailure
      ? Result.Failure<AuthorizedCompanyScope>(EmployeeErrors.CompanyScopeDenied)
      : Result.Success(AuthorizedCompanyScope.Create([selected]));
  }

  private async Task<Result<AuthorizedBranchScope>> ResolveBranchesAsync(
    EmployeeScopeRequest request, Guid tenantId, long tenantUserId, CancellationToken cancellationToken)
  {
    // A branch list supplied for any other mode is a MALFORMED REQUEST, not something to ignore. Ignoring it
    // would let a caller believe they had narrowed a read that in fact ran wider.
    if (request.BranchScope != EmployeeBranchScopeMode.SelectedAuthorizedBranches &&
      request.SelectedBranchIds is { Count: > 0 })
    {
      return Result.Failure<AuthorizedBranchScope>(EmployeeErrors.InvalidReadScope);
    }

    switch (request.BranchScope)
    {
      case EmployeeBranchScopeMode.CurrentBranch:
      {
        // The trusted execution branch, re-read from the durable session and re-authorized against live
        // state — the same answer the write boundary uses, so a read and a write cannot disagree.
        var current = await currentBranch.ResolveCurrentBranchAsync(cancellationToken);

        return current.IsFailure
          ? Result.Failure<AuthorizedBranchScope>(EmployeeErrors.BranchScopeDenied)
          : Result.Success(AuthorizedBranchScope.Create([current.Value]));
      }

      case EmployeeBranchScopeMode.SelectedAuthorizedBranches:
      {
        var requested = request.SelectedBranchIds?.Distinct().ToArray() ?? [];
        if (requested.Length == 0 || requested.Contains(Guid.Empty))
        {
          return Result.Failure<AuthorizedBranchScope>(EmployeeErrors.InvalidReadScope);
        }

        // ---- SUBSET, OR REFUSE. Every requested branch is authorized INDIVIDUALLY against live state; a
        // request naming even one branch outside the authorized set is refused rather than quietly
        // intersected, so a caller is never told they saw everything they asked for when they did not.
        //
        // Each refusal is the resolver's own generic error, so an unauthorized, inactive and nonexistent
        // branch are indistinguishable and the read path cannot be used to probe for existence.
        foreach (var branchId in requested)
        {
          var authorized = await branchAccess.AuthorizeBranchAsync(
            tenantId, tenantUserId, branchId, cancellationToken);
          if (authorized.IsFailure)
          {
            return Result.Failure<AuthorizedBranchScope>(EmployeeErrors.BranchScopeDenied);
          }
        }

        return Result.Success(AuthorizedBranchScope.Create(requested));
      }

      case EmployeeBranchScopeMode.AllAuthorizedBranches:
      {
        var permitted = await branchAccess.GetPermittedBranchesAsync(tenantId, tenantUserId, cancellationToken);
        if (permitted.IsFailure)
        {
          return Result.Failure<AuthorizedBranchScope>(EmployeeErrors.BranchScopeDenied);
        }

        var branchIds = permitted.Value.Select(branch => branch.BranchId).ToArray();

        // MATERIALIZED, NEVER OMITTED. "All branches" is this list; an empty one refuses the read.
        return branchIds.Length == 0
          ? Result.Failure<AuthorizedBranchScope>(EmployeeErrors.BranchScopeDenied)
          : Result.Success(AuthorizedBranchScope.Create(branchIds));
      }

      default:
        return Result.Failure<AuthorizedBranchScope>(EmployeeErrors.InvalidReadScope);
    }
  }
}
