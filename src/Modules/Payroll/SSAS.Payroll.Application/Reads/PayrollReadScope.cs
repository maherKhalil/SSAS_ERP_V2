using SSAS.BuildingBlocks.Application.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;

namespace SSAS.Payroll.Application.Reads;

// ================================================================================================
// THE THIRD CONSUMER. THE PROMOTION IT TRIGGERED WAS RULED AND PERFORMED ON 2026-08-24.
// ================================================================================================
//
// `GlReadScope` wrote the condition where it would be found, and Payroll met it:
//
//   > **THE TRIGGER, WRITTEN WHERE IT WILL BE FOUND: a THIRD consumer.** Two modules each carrying a guarded
//   > identifier list is duplication nobody trips over. Three is where the shapes start to drift, and drift
//   > in a scope type is a SECURITY DEFECT rather than an inconvenience.
//
// HR's `AuthorizedCompanyScope` was one, `GlReadScope` two, this three. The promotion was raised rather than
// taken at the keyboard — `ADR-027` decision 4 makes it a reviewed change to shared foundations — and the
// review then ruled it. `AuthorizedCompanySet` in `SSAS.BuildingBlocks.Application.Authorization` is the
// result.
//
// ---- WHAT MOVED, AND WHAT DELIBERATELY DID NOT.
//
// **The VALUE moved**: the materialized, never-empty, never-writeable company set.
//
// **This TYPE did not.** Its constructor is private, its factory is `internal`, and its only caller is
// `PayrollScopeResolver`. Holding one is proof that PAYROLL's permission check and PAYROLL's company
// resolution both ran against live state — a shared scope type would let any module that could build one
// hand it to any other module's read service, and the proof would become a shrug.
//
// **On this surface that matters more than anywhere it has mattered before.** Everywhere else a forgeable
// scope is an authorization defect; for compensation it is a personal-data breach.
//
// An empty authorized set REFUSES the read rather than returning an empty page. An empty page says "there is
// nothing here", a claim about the data; a refusal says "you cannot see", a claim about the caller. Only the
// second is true, and only the second stays true when someone later grants the caller a company.
public sealed class PayrollReadScope
{
  private PayrollReadScope(Guid tenantId, AuthorizedCompanySet companies)
  {
    TenantId = tenantId;
    Companies = companies;
  }

  // Carried so a query STATES the invariant it depends on rather than inheriting it from the global filter.
  public Guid TenantId { get; }

  // The promoted set (`ADR-027` d4). The wrapper is what carries the proof; this is only the data.
  public AuthorizedCompanySet Companies { get; }

  public IReadOnlyList<Guid> CompanyIds => Companies.CompanyIds;

  // `internal`, single caller. The empty check lives here rather than in the resolver so it holds for every
  // future caller of the factory, not merely the one that exists today.
  internal static PayrollReadScope? Create(Guid tenantId, IReadOnlyList<Guid> companyIds)
  {
    ArgumentNullException.ThrowIfNull(companyIds);

    if (tenantId == Guid.Empty)
    {
      return null;
    }

    var companies = AuthorizedCompanySet.Create(companyIds);
    return companies is null ? null : new PayrollReadScope(tenantId, companies);
  }
}

public interface IPayrollScopeResolver
{
  // The functional permission is a PARAMETER, not a constant, because Payroll has several read surfaces with
  // materially different authority — elements are structural, compensation and payslips are personal data.
  // A resolver that hard-coded one would either check the wrong permission or tempt a caller to bypass it
  // for the others, and on this surface the second failure is a data breach.
  Task<Result<PayrollReadScope>> ResolveAsync(
    string permissionName, CancellationToken cancellationToken = default);

  // Writes name exactly ONE company and must prove the caller may reach that one. A separate method rather
  // than reusing the read path, because a write that filtered by a set would be asking a different question
  // than the one it needs answered.
  Task<Result> AuthorizeAsync(
    string permissionName, Guid companyId, CancellationToken cancellationToken = default);

  Result RequirePermission(string permissionName);
}

// THE ONLY PLACE A `PayrollReadScope` COMES FROM.
//
// Every check reads LIVE state — `ITenantCompanyAccessResolver` answers "which companies may this user reach
// RIGHT NOW", not a value the caller supplied and not one cached from an earlier request. A grant revoked a
// moment ago must refuse the read in flight.
//
// The two axes are independent and neither widens the other: the functional permission says which OPERATION
// is permitted, the company set says which data is reachable. `Platform.Tenant.Administer` widens the second
// and grants none of the first, so a tenant administrator without `Payroll.Compensation.View` reads no
// compensation at all.
public sealed class PayrollScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IPayrollScopeResolver
{
  public Result RequirePermission(string permissionName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    return currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal)
      ? Result.Success()
      : Result.Failure(PayrollScopeErrors.WritePermissionDenied);
  }

  public async Task<Result> AuthorizeAsync(
    string permissionName, Guid companyId, CancellationToken cancellationToken = default)
  {
    // Permission first, so an unauthorized caller never causes a company lookup: the cheap refusal happens
    // before the expensive one.
    var permitted = RequirePermission(permissionName);
    if (permitted.IsFailure)
    {
      return permitted;
    }

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(PayrollScopeErrors.InvalidActor);
    }

    var companies = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
    if (companies.IsFailure)
    {
      return Result.Failure(PayrollScopeErrors.CompanyScopeDenied);
    }

    return companies.Value.Any(company => company.CompanyId == companyId)
      ? Result.Success()
      : Result.Failure(PayrollScopeErrors.CompanyScopeDenied);
  }

  public async Task<Result<PayrollReadScope>> ResolveAsync(
    string permissionName, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<PayrollReadScope>(PayrollScopeErrors.InvalidActor);
    }

    if (!currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal))
    {
      return Result.Failure<PayrollReadScope>(PayrollScopeErrors.ReadPermissionDenied);
    }

    var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
    if (permitted.IsFailure)
    {
      return Result.Failure<PayrollReadScope>(PayrollScopeErrors.CompanyScopeDenied);
    }

    var companyIds = permitted.Value.Select(company => company.CompanyId).ToArray();
    var scope = PayrollReadScope.Create(tenantId, companyIds);

    return scope is null
      ? Result.Failure<PayrollReadScope>(PayrollScopeErrors.CompanyScopeDenied)
      : Result.Success(scope);
  }
}

// Scope refusals name NO company, NO tenant and NO database topology. A caller who cannot reach a company
// learns only that they cannot, never whether the identifier they guessed exists.
public static class PayrollScopeErrors
{
  public static readonly Error InvalidActor = new(
    "Payroll.InvalidActor",
    "The request does not carry a resolved tenant user.");

  public static readonly Error ReadPermissionDenied = new(
    "Payroll.ReadPermissionDenied",
    "The caller does not hold the permission required for this read.");

  public static readonly Error WritePermissionDenied = new(
    "Payroll.WritePermissionDenied",
    "The caller does not hold the permission required for this operation.");

  public static readonly Error CompanyScopeDenied = new(
    "Payroll.CompanyScopeDenied",
    "The caller has no authorized company scope for this read.");
}
