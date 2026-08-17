using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Branches;

// Answers "are all of these assignable right now" from the tenant database (Branch foundation B1b).
internal sealed class TenantBranchValidator(ITenantDbContextFactory tenantContextFactory) : ITenantBranchValidator
{
  public async Task<Result> ValidateAssignableAsync(
    Guid tenantId,
    IReadOnlyCollection<Guid> branchIds,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(branchIds);

    if (tenantId == Guid.Empty || branchIds.Count == 0)
    {
      return Result.Failure(BranchErrors.AssignmentInvalid);
    }

    var requested = branchIds.Distinct().ToArray();

    // DUPLICATES ARE A MALFORMED REQUEST, not something to quietly collapse. The unique index would refuse
    // the second row anyway; refusing here names the reason instead of surfacing a persistence conflict.
    if (requested.Length != branchIds.Count || requested.Contains(Guid.Empty))
    {
      return Result.Failure(BranchErrors.AssignmentInvalid);
    }

    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      // Tenant storage being unreachable is NOT "these branches are invalid": failing closed with the
      // storage error keeps an outage from looking like a bad request and being retried as one.
      return Result.Failure(context.Error);
    }

    await using var tenant = context.Value;

    // COUNTED, NOT LISTED. Only the number of requested branches that are genuinely active in THIS tenant
    // matters; returning which ones failed would answer questions about other tenants' identifiers.
    var assignable = await tenant.Branches
      .AsNoTracking()
      .CountAsync(
        branch => branch.TenantId == tenantId && branch.IsActive && requested.Contains(branch.Id),
        cancellationToken);

    return assignable == requested.Length
      ? Result.Success()
      : Result.Failure(BranchErrors.AssignmentInvalid);
  }
}
