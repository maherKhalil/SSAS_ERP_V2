using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions.Reads;

// WHICH COMPANIES THE CALLER IS ASKING FOR (FP-008 Phase 2, ADR-025 decision 10).
//
// A CLOSED CHOICE, so "no company specified" is unrepresentable in the read contract.
public enum PositionCompanyScopeMode
{
  // The selected company, proven by the live validation before it is used.
  CurrentCompany = 0,

  // Every company currently authorized to this caller, materialized. Never the absence of a predicate.
  AllAuthorizedCompanies = 1
}

// The caller's INTENT. It carries no authority: the resolver turns it into a scope only after proving the
// functional permission and the company dimension against live state, and refuses otherwise.
public sealed record PositionScopeRequest(
  PositionCompanyScopeMode CompanyScope = PositionCompanyScopeMode.CurrentCompany);

// THE ONLY WAY TO OBTAIN ANY OF THE THREE POSITION-FAMILY READ SCOPES (FP-008 Phase 2).
//
// ---- ONE RESOLVER, THREE TYPED METHODS, AND WHY IT IS NOT THREE RESOLVERS.
//
// The company dimension is one question with one answer, resolved from one authority. Three resolvers would
// be three copies of that resolution, and the failure mode of three copies is that one of them stops
// refusing an empty set. What genuinely differs per family is the FUNCTIONAL permission, and that is a
// single constant per method — so the difference lives where it is visible rather than being spread across
// three near-identical classes.
//
// Each method checks its OWN `View` permission and returns its OWN scope type, so the separation
// `DEC-POS-0018` bought with `HR.SalaryGrades.View` is enforced by the compiler rather than by convention.
public interface IPositionScopeResolver
{
  Task<Result<PositionReadScope>> ResolvePositionsAsync(
    PositionScopeRequest request, CancellationToken cancellationToken = default);

  Task<Result<JobGradeReadScope>> ResolveJobGradesAsync(
    PositionScopeRequest request, CancellationToken cancellationToken = default);

  // Requires `HR.SalaryGrades.View`. A caller holding every other HR permission still cannot obtain one.
  Task<Result<SalaryGradeReadScope>> ResolveSalaryGradesAsync(
    PositionScopeRequest request, CancellationToken cancellationToken = default);

  // The WRITE-side equivalent: prove the functional permission and that this specific company is reachable.
  // Same two questions, asked of one company rather than a set, so a read path and a write path cannot
  // disagree about what "authorized" means.
  Task<Result> AuthorizeAsync(
    string permission, Guid companyId, CancellationToken cancellationToken = default);

  // Functional permission alone, for the point in a write where the company is not yet known — it comes
  // from the record being loaded. Company scope is then proven separately by AuthorizeAsync.
  Result RequirePermission(string permission);
}

// ---- IT CHECKS BOTH DIMENSIONS, INDEPENDENTLY, AND REFUSES IF EITHER FAILS.
//
// Functional permission and authorized company scope are separate questions with separate answers (ADR-025
// decision 8). They are asked in one place so that no path can forget one — not so that one can stand in
// for another. `Platform.Tenant.Administer` answers the SCOPE question by widening the company set and
// answers nothing about the PERMISSION question: an administrator without `HR.Positions.View` cannot read a
// position.
//
// ---- IT RESOLVES LIVE, EVERY TIME (NFR-POS-0303).
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
// ---- THERE IS NO BRANCH DIMENSION, AND ITS ABSENCE IS DELIBERATE (DEC-POS-0020).
//
// A Position is not branch-owned, so branch scope does not determine whether one is VISIBLE. Employee
// membership remains branch-scoped by the Employee read path, which is untouched.
public sealed class PositionScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ICurrentCompany currentCompany,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IPositionScopeResolver
{
  public async Task<Result<PositionReadScope>> ResolvePositionsAsync(
    PositionScopeRequest request, CancellationToken cancellationToken = default)
  {
    var resolved = await ResolveAsync(
      request, HrPermissionNames.ViewPositions, cancellationToken);

    return resolved.IsFailure
      ? Result.Failure<PositionReadScope>(resolved.Error)
      : Result.Success(PositionReadScope.Create(resolved.Value.TenantId, resolved.Value.Companies));
  }

  public async Task<Result<JobGradeReadScope>> ResolveJobGradesAsync(
    PositionScopeRequest request, CancellationToken cancellationToken = default)
  {
    var resolved = await ResolveAsync(
      request, HrPermissionNames.ViewJobGrades, cancellationToken);

    return resolved.IsFailure
      ? Result.Failure<JobGradeReadScope>(resolved.Error)
      : Result.Success(JobGradeReadScope.Create(resolved.Value.TenantId, resolved.Value.Companies));
  }

  public async Task<Result<SalaryGradeReadScope>> ResolveSalaryGradesAsync(
    PositionScopeRequest request, CancellationToken cancellationToken = default)
  {
    var resolved = await ResolveAsync(
      request, HrPermissionNames.ViewSalaryGrades, cancellationToken);

    return resolved.IsFailure
      ? Result.Failure<SalaryGradeReadScope>(resolved.Error)
      : Result.Success(SalaryGradeReadScope.Create(resolved.Value.TenantId, resolved.Value.Companies));
  }

  public async Task<Result> AuthorizeAsync(
    string permission, Guid companyId, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(PositionErrors.InvalidActor);
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

    return authorized.IsFailure ? Result.Failure(PositionErrors.CompanyScopeDenied) : Result.Success();
  }

  public Result RequirePermission(string permission) =>
    currentUser.Permissions.Contains(permission, StringComparer.Ordinal)
      ? Result.Success()
      : Result.Failure(PositionErrors.PermissionDenied);

  // The shared half: the two dimensions, in order, producing the tenant and the company set that each
  // public method then stamps into its own scope type.
  private async Task<Result<ResolvedScope>> ResolveAsync(
    PositionScopeRequest request, string viewPermission, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<ResolvedScope>(PositionErrors.InvalidActor);
    }

    // ---- DIMENSION 1: THE FUNCTIONAL PERMISSION. Asked FIRST and asked alone.
    var permitted = RequirePermission(viewPermission);
    if (permitted.IsFailure)
    {
      return Result.Failure<ResolvedScope>(permitted.Error);
    }

    // ---- DIMENSION 2: THE AUTHORIZED COMPANY SET.
    var companies = await ResolveCompaniesAsync(request, tenantId, tenantUserId, cancellationToken);

    return companies.IsFailure
      ? Result.Failure<ResolvedScope>(companies.Error)
      : Result.Success(new ResolvedScope(tenantId, companies.Value));
  }

  private async Task<Result<AuthorizedPositionCompanyScope>> ResolveCompaniesAsync(
    PositionScopeRequest request, Guid tenantId, long tenantUserId, CancellationToken cancellationToken)
  {
    if (request.CompanyScope == PositionCompanyScopeMode.AllAuthorizedCompanies)
    {
      var permitted = await companyAccess.GetPermittedCompaniesAsync(
        tenantId, tenantUserId, cancellationToken);
      if (permitted.IsFailure)
      {
        return Result.Failure<AuthorizedPositionCompanyScope>(PositionErrors.CompanyScopeDenied);
      }

      var companyIds = permitted.Value.Select(company => company.CompanyId).ToArray();

      // AN EMPTY AUTHORIZED SET REFUSES. It never degrades to unfiltered, and it never becomes an empty
      // predicate that a later reader might "optimise away".
      return companyIds.Length == 0
        ? Result.Failure<AuthorizedPositionCompanyScope>(PositionErrors.CompanyScopeDenied)
        : Result.Success(AuthorizedPositionCompanyScope.Create(companyIds));
    }

    // The selected company is INTENT until the resolver proves it: exists, belongs to this tenant, is
    // Active, and is reachable by this caller. It is re-asked here rather than trusted.
    if (currentCompany.CompanyId is not { } selected || selected == Guid.Empty)
    {
      return Result.Failure<AuthorizedPositionCompanyScope>(PositionErrors.CompanyScopeDenied);
    }

    var authorized = await companyAccess.AuthorizeCompanyAsync(
      tenantId, tenantUserId, selected, cancellationToken);

    return authorized.IsFailure
      ? Result.Failure<AuthorizedPositionCompanyScope>(PositionErrors.CompanyScopeDenied)
      : Result.Success(AuthorizedPositionCompanyScope.Create([selected]));
  }

  // Private, and never returned across the interface: it is the shared half of a resolution, not a scope.
  // Nothing outside this class can turn one into a read.
  private readonly record struct ResolvedScope(Guid TenantId, AuthorizedPositionCompanyScope Companies);
}
