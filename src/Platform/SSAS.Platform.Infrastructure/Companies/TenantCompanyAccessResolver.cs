using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Companies;

// THE COMPANY SCOPE OF ONE USER (FP-006C1, ADR-025 decision 6).
//
// IT READS BOTH PLANES, IN THAT ORDER AND FOR DIFFERENT REASONS. Authority and assignments live in the
// platform database; the companies themselves live in the tenant database. There is no join between them —
// there cannot be, they are different catalogs and may be different servers — so this reads the tenant's
// Active companies and intersects in memory against the platform-side assignment set.
//
// THE INTERSECTION IS SMALL BY CONSTRUCTION: a tenant's company list is a legal-entity list, not a
// transaction table. Nothing here scans business data.
//
// IT MIRRORS TenantBranchAccessResolver DELIBERATELY, because ADR-025 chose the branch pattern for the
// sibling dimension. It does NOT mirror the parts branch needs for its own reasons: there is no
// minimum-one-company invariant (no authority establishes one), so an empty permitted set is an ordinary
// answer here rather than an account-integrity failure.
internal sealed class TenantCompanyAccessResolver(
  PlatformDbContext platform,
  ITenantDbContextFactory tenantContextFactory,
  ITenantAdministratorAuthority administratorAuthority) : ITenantCompanyAccessResolver
{
  public async Task<Result<IReadOnlyList<CompanyAccessSummary>>> GetPermittedCompaniesAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0)
    {
      return Result.Failure<IReadOnlyList<CompanyAccessSummary>>(CompanyAccessErrors.InvalidSelection);
    }

    var active = await ReadActiveCompaniesAsync(tenantId, cancellationToken);
    if (active.IsFailure)
    {
      return Result.Failure<IReadOnlyList<CompanyAccessSummary>>(active.Error);
    }

    // A TENANT ADMINISTRATOR'S SCOPE IS THE TENANT. Held implicitly, so a company created a moment ago is
    // already reachable and no assignment rows have to be synchronised into existence — the same
    // bootstrapping argument that keeps the first administrator able to create the first company.
    if (await administratorAuthority.IsTenantAdministratorAsync(tenantId, tenantUserId, cancellationToken))
    {
      return Result.Success(active.Value);
    }

    var assigned = await platform.UserCompanyAccess
      .AsNoTracking()
      .Where(access => access.TenantId == tenantId && access.TenantUserId == tenantUserId)
      .Select(access => access.CompanyId)
      .ToListAsync(cancellationToken);

    var permitted = assigned.Count == 0
      ? []
      : active.Value.Where(company => assigned.Contains(company.CompanyId)).ToArray();

    return Result.Success<IReadOnlyList<CompanyAccessSummary>>(permitted);
  }

  // ASKED AGAIN, AGAINST THE DATABASE, EVERY TIME. Request-context establishment and company-owned writes
  // both land here rather than consulting a set captured at login — access is revocable and companies are
  // deactivatable inside a request, and a write admitted on a stale set is the failure this prevents.
  public async Task<Result> AuthorizeCompanyAsync(
    Guid tenantId,
    long tenantUserId,
    Guid companyId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0 || companyId == Guid.Empty)
    {
      return Result.Failure(CompanyAccessErrors.InvalidSelection);
    }

    var companyIsActive = await TenantCompanyIsActiveAsync(tenantId, companyId, cancellationToken);
    if (companyIsActive.IsFailure)
    {
      return Result.Failure(companyIsActive.Error);
    }

    // ONE GENERIC REFUSAL. "No such company", "another tenant's company" and "not Active" are answered
    // identically so a caller cannot probe for the existence of companies it may not see.
    if (!companyIsActive.Value)
    {
      return Result.Failure(CompanyAccessErrors.InvalidSelection);
    }

    if (await administratorAuthority.IsTenantAdministratorAsync(tenantId, tenantUserId, cancellationToken))
    {
      return Result.Success();
    }

    var assigned = await platform.UserCompanyAccess
      .AsNoTracking()
      .AnyAsync(
        access => access.TenantId == tenantId &&
          access.TenantUserId == tenantUserId &&
          access.CompanyId == companyId,
        cancellationToken);

    // The unauthorized case collapses into the SAME error as nonexistent and inactive, deliberately.
    return assigned ? Result.Success() : Result.Failure(CompanyAccessErrors.InvalidSelection);
  }

  private async Task<Result<IReadOnlyList<CompanyAccessSummary>>> ReadActiveCompaniesAsync(
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      // Tenant storage being unavailable is NOT "no companies": answering with an empty list would look
      // exactly like a tenant that has not created one yet, and would silently deny every company-owned
      // operation as though it were an authorization outcome.
      return Result.Failure<IReadOnlyList<CompanyAccessSummary>>(context.Error);
    }

    await using var tenant = context.Value;
    var companies = await tenant.Companies
      .AsNoTracking()
      .Where(company => company.Status == CompanyStatus.Active)
      .OrderBy(company => company.CompanyName)
      .Select(company => new CompanyAccessSummary(
        company.Id, company.CompanyCode.Value, company.CompanyName.Value))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<CompanyAccessSummary>>(companies);
  }

  private async Task<Result<bool>> TenantCompanyIsActiveAsync(
    Guid tenantId,
    Guid companyId,
    CancellationToken cancellationToken)
  {
    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<bool>(context.Error);
    }

    await using var tenant = context.Value;

    // The tenant global query filter already restricts this to the routed tenant; TenantId is compared
    // explicitly as well so the predicate states the invariant it depends on rather than inheriting it.
    var isActive = await tenant.Companies
      .AsNoTracking()
      .AnyAsync(
        company => company.Id == companyId &&
          company.TenantId == tenantId &&
          company.Status == CompanyStatus.Active,
        cancellationToken);

    return Result.Success(isActive);
  }
}
