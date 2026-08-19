using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;

namespace SSAS.Platform.Infrastructure.Branches;

// The module-facing read of the trusted execution branch (FP-006C3, ADR-023).
//
// IT DELEGATES; IT DECIDES NOTHING. Everything that makes the answer trustworthy — the durable session, its
// status and expiry, and the live access resolver — lives in IBranchWriteAuthorizer, and this is the same
// call the write boundary makes. A second implementation would be a second opinion, and the whole point is
// that a module and the boundary cannot disagree about which branch is current.
internal sealed class CurrentBranchResolver(
  IBranchWriteAuthorizer branchWriteAuthorizer,
  ICurrentTenant currentTenant) : ICurrentBranchResolver
{
  public async Task<Result<Guid>> ResolveCurrentBranchAsync(CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
    {
      return Result.Failure<Guid>(BranchErrors.ContextRequired);
    }

    return await branchWriteAuthorizer.AuthorizeCurrentBranchAsync(tenantId, cancellationToken);
  }
}
