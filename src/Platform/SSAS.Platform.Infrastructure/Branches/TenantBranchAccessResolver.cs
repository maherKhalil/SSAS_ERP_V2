using SSAS.BuildingBlocks.Tenancy.Branches;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Branches;

// THE BRANCH SCOPE OF ONE USER (Branch foundation B0/B1).
//
// IT READS BOTH PLANES, IN THAT ORDER AND FOR DIFFERENT REASONS. Authority and assignments live in the
// platform database; the branches themselves live in the tenant database. There is no join between them —
// there cannot be, they are different catalogs and may be different servers — so this reads the tenant's
// active branches and intersects in memory against the platform-side assignment set.
//
// THE INTERSECTION IS SMALL BY CONSTRUCTION: a tenant's branch list is an operating-locations list, not a
// transaction table. Nothing here scans business data.
internal sealed class TenantBranchAccessResolver(
  PlatformDbContext platform,
  ITenantDbContextFactory tenantContextFactory,
  ITenantAdministratorAuthority administratorAuthority) : ITenantBranchAccessResolver
{
  public async Task<Result<IReadOnlyList<BranchAccessSummary>>> GetPermittedBranchesAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0)
    {
      return Result.Failure<IReadOnlyList<BranchAccessSummary>>(BranchErrors.InvalidSelection);
    }

    var active = await ReadActiveBranchesAsync(tenantId, cancellationToken);
    if (active.IsFailure)
    {
      return Result.Failure<IReadOnlyList<BranchAccessSummary>>(active.Error);
    }

    // A TENANT ADMINISTRATOR'S SCOPE IS THE TENANT. Held implicitly, so a branch created a moment ago is
    // already reachable and no assignment rows have to be synchronised into existence.
    if (await administratorAuthority.IsTenantAdministratorAsync(tenantId, tenantUserId, cancellationToken))
    {
      return Result.Success(active.Value);
    }

    var assigned = await platform.UserBranchAccess
      .AsNoTracking()
      .Where(access => access.TenantId == tenantId && access.TenantUserId == tenantUserId)
      .Select(access => access.BranchId)
      .ToListAsync(cancellationToken);

    var permitted = assigned.Count == 0
      ? []
      : active.Value.Where(branch => assigned.Contains(branch.BranchId)).ToArray();

    return Result.Success<IReadOnlyList<BranchAccessSummary>>(permitted);
  }

  // ASKED AGAIN, AGAINST THE DATABASE, EVERY TIME. Selection, switching and branch-owned writes all land
  // here rather than consulting a list captured at login — access is revocable and branches are
  // deactivatable inside a session, and a write admitted on a stale list is the failure this prevents.
  public async Task<Result> AuthorizeBranchAsync(
    Guid tenantId,
    long tenantUserId,
    Guid branchId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0 || branchId == Guid.Empty)
    {
      return Result.Failure(BranchErrors.InvalidSelection);
    }

    var branchIsActive = await TenantBranchIsActiveAsync(tenantId, branchId, cancellationToken);
    if (branchIsActive.IsFailure)
    {
      return Result.Failure(branchIsActive.Error);
    }

    // ONE GENERIC REFUSAL. "No such branch", "another tenant's branch" and "inactive" are answered
    // identically so a caller cannot probe for the existence of branches it may not see.
    if (!branchIsActive.Value)
    {
      return Result.Failure(BranchErrors.InvalidSelection);
    }

    if (await administratorAuthority.IsTenantAdministratorAsync(tenantId, tenantUserId, cancellationToken))
    {
      return Result.Success();
    }

    var assigned = await platform.UserBranchAccess
      .AsNoTracking()
      .AnyAsync(
        access => access.TenantId == tenantId &&
          access.TenantUserId == tenantUserId &&
          access.BranchId == branchId,
        cancellationToken);

    return assigned ? Result.Success() : Result.Failure(BranchErrors.InvalidSelection);
  }

  private async Task<Result<IReadOnlyList<BranchAccessSummary>>> ReadActiveBranchesAsync(
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      // Tenant storage being unavailable is NOT "no branches": answering with an empty list would look
      // exactly like a tenant awaiting its first branch and would send an administrator to onboarding.
      return Result.Failure<IReadOnlyList<BranchAccessSummary>>(context.Error);
    }

    await using var tenant = context.Value;
    var branches = await tenant.Branches
      .AsNoTracking()
      .Where(branch => branch.IsActive)
      .OrderBy(branch => branch.BranchName)
      .Select(branch => new BranchAccessSummary(
        branch.Id, branch.BranchCode.Value, branch.BranchName.Value, branch.IsMainBranch))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<BranchAccessSummary>>(branches);
  }

  private async Task<Result<bool>> TenantBranchIsActiveAsync(
    Guid tenantId,
    Guid branchId,
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
    var isActive = await tenant.Branches
      .AsNoTracking()
      .AnyAsync(branch => branch.Id == branchId && branch.TenantId == tenantId && branch.IsActive, cancellationToken);

    return Result.Success(isActive);
  }
}
