using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.GL.Application.Permissions;

namespace SSAS.GL.Application.Reads;

public interface IGlScopeResolver
{
  // `permissionName` is the FUNCTIONAL permission the caller must hold for the read they are attempting.
  // It is a parameter rather than a constant because GL has several read surfaces with different authority
  // — journals, the chart, the calendar, reports — and a resolver that hard-coded one of them would either
  // check the wrong permission or tempt a caller to skip the resolver for the others.
  Task<Result<GlReadScope>> ResolveAsync(string permissionName, CancellationToken cancellationToken = default);

  // ---- THE WRITE SIDE. Both dimensions, for a write against ONE named company.
  //
  // Reads resolve a SET of companies and filter by it; a write names exactly one and must prove the caller
  // may reach that one. Separate methods rather than one, because a write that filtered by a set would be
  // asking a different question than the one it needs answered.
  Task<Result> AuthorizeAsync(
    string permissionName, Guid companyId, CancellationToken cancellationToken = default);

  // ---- FUNCTIONAL PERMISSION ALONE, AND OD-GL-0003 IS WHY IT IS NOT A SHORTCUT.
  //
  // HR offers this for the point in a write where the company is not yet known because it comes from the
  // entity being loaded. GL has that case too — and it also has a case HR does not: the CHART OF ACCOUNTS
  // IS TENANT-LEVEL, so an account write has no company to authorize against at all. For those handlers
  // this is not a partial check awaiting a second one; it is the whole check, and the absence of a company
  // dimension is the ruling rather than an omission.
  Result RequirePermission(string permissionName);
}

// THE ONLY PLACE A `GlReadScope` COMES FROM (DEC-GL-0004).
//
// The scope type's factory is internal and this is its single caller, so possessing a scope is proof that
// this method ran and refused nothing. Every check reads LIVE state — `ITenantCompanyAccessResolver` is the
// platform's answer to "which companies may this user reach right now", not a value the caller supplied and
// not one cached from an earlier request.
//
// ---- THE TWO AXES ARE CHECKED INDEPENDENTLY, AND NEITHER WIDENS THE OTHER.
//
// The functional permission says which OPERATION is permitted. The company set says which data is
// reachable. `Platform.Tenant.Administer` widens the second and grants none of the first, so an
// administrator without `GL.Journals.View` resolves no scope and reads nothing (`ADR-025` decision 8,
// `AC-GL-0017`). Checking the permission FIRST also means an unauthorized caller never causes a company
// lookup, so the cheap refusal happens before the expensive one.
public sealed class GlScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IGlScopeResolver
{
  public Result RequirePermission(string permissionName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    return currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal)
      ? Result.Success()
      : Result.Failure(GlScopeErrors.WritePermissionDenied);
  }

  public async Task<Result> AuthorizeAsync(
    string permissionName, Guid companyId, CancellationToken cancellationToken = default)
  {
    var permitted = RequirePermission(permissionName);
    if (permitted.IsFailure)
    {
      return permitted;
    }

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    // Resolved live, never trusted from the request. The company the caller named is only reachable if the
    // platform says so RIGHT NOW — a grant revoked a moment ago must refuse the write in flight.
    var companies = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
    if (companies.IsFailure)
    {
      return Result.Failure(GlScopeErrors.CompanyScopeDenied);
    }

    return companies.Value.Any(company => company.CompanyId == companyId)
      ? Result.Success()
      : Result.Failure(GlScopeErrors.CompanyScopeDenied);
  }

  public async Task<Result<GlReadScope>> ResolveAsync(
    string permissionName, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<GlReadScope>(GlScopeErrors.InvalidActor);
    }

    if (!currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal))
    {
      return Result.Failure<GlReadScope>(GlScopeErrors.ReadPermissionDenied);
    }

    var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
    if (permitted.IsFailure)
    {
      return Result.Failure<GlReadScope>(GlScopeErrors.CompanyScopeDenied);
    }

    var companyIds = permitted.Value.Select(company => company.CompanyId).ToArray();

    // ---- AN EMPTY AUTHORIZED SET REFUSES THE READ. It does not return an empty page.
    //
    // The distinction is the whole of `AC-GL-0014`. An empty page says "there is nothing here", which is a
    // claim about the data; a refusal says "you cannot see", which is a claim about the caller. Only the
    // second is true, and only the second stays true when someone later grants the caller a company.
    var scope = GlReadScope.Create(tenantId, companyIds);

    return scope is null
      ? Result.Failure<GlReadScope>(GlScopeErrors.CompanyScopeDenied)
      : Result.Success(scope);
  }
}

// Scope refusals name NO company, NO tenant and NO database topology. A caller who cannot reach a company
// learns only that they cannot, never whether the identifier they guessed exists — the same posture
// `JournalErrors` takes for an out-of-scope account, and the reason `Gl.AccountNotFound` is preferred over
// a "forbidden" code.
public static class GlScopeErrors
{
  public static readonly Error InvalidActor = new(
    "Gl.InvalidActor",
    "The request does not carry a resolved tenant user.");

  public static readonly Error ReadPermissionDenied = new(
    "Gl.ReadPermissionDenied",
    "The caller does not hold the permission required for this read.");

  public static readonly Error WritePermissionDenied = new(
    "Gl.WritePermissionDenied",
    "The caller does not hold the permission required for this operation.");

  public static readonly Error CompanyScopeDenied = new(
    "Gl.CompanyScopeDenied",
    "The caller has no authorized company scope for this read.");
}
