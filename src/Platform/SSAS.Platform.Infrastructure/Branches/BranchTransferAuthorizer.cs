using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Branches;

// Re-validates the open branch-transfer declaration on every save (FP-006C2, ADR-024 decisions 3, 6 and 12).
//
// IT RE-ASKS; IT DOES NOT REMEMBER. Everything below is read from authoritative state at the moment of the
// save, because a declaration opened earlier in the operation proves only that the authorization existed
// then — and access, branch state and administrator authority can all change in between.
internal sealed class BranchTransferAuthorizer(
  IBranchTransferScope transferScope,
  ITenantBranchAccessResolver accessResolver,
  ITenantAdministratorAuthority administratorAuthority,
  ITenantDbContextFactory tenantContextFactory,
  // OPTIONAL, AND ITS ABSENCE IS A REFUSAL — the same reasoning BranchWriteAuthorizer records. Background
  // and maintenance compositions have no session, so there is nobody to authorize and no transfer.
  ICurrentAuthenticationSession? currentSession = null) : IBranchTransferAuthorizer
{
  public async Task<Result<BranchTransferDeclaration?>> AuthorizeOpenTransferAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    // NO DECLARATION IS NOT A FAILURE. It is the answer "no transfer is authorized", and the ordinary rules
    // then refuse every BranchId modification exactly as before.
    if (transferScope.Current is not { } declaration)
    {
      return Result.Success<BranchTransferDeclaration?>(null);
    }

    if (tenantId == Guid.Empty ||
      currentSession?.Value is not { } session ||
      session.TenantId != tenantId ||
      session.TenantUserId <= 0)
    {
      return Result.Failure<BranchTransferDeclaration?>(BranchErrors.TransferNotPermitted);
    }

    // ---- THE DESTINATION IS AUTHORIZED HERE, AND ONLY THROUGH THE RESOLVER.
    //
    // The declaration naming a destination is not authorization for it: that is the whole point of asking
    // again. The resolver intersects with ACTIVE branches, so a deactivated destination and a revoked
    // assignment are both refused — and its generic refusal is returned unchanged, so the transfer path
    // cannot be used to probe destination identifiers for existence.
    var destination = await accessResolver.AuthorizeBranchAsync(
      tenantId, session.TenantUserId, declaration.DestinationBranchId, cancellationToken);
    if (destination.IsFailure)
    {
      return Result.Failure<BranchTransferDeclaration?>(destination.Error);
    }

    // The ordinary mode's source is the caller's execution branch. The write boundary establishes that
    // through IBranchWriteAuthorizer and then requires the declared source to equal it, so there is exactly
    // one place that decides what the execution branch is.
    if (declaration.Mode != BranchTransferMode.InactiveSourceRecovery)
    {
      return Result.Success<BranchTransferDeclaration?>(declaration);
    }

    // ---- THE NARROW RECOVERY (ADR-024 decision 12), RE-VALIDATED IN FULL.
    //
    // Both facts are re-read. Authority alone is not enough: without the inactive check an administrator
    // could use recovery mode to move an entity out of an ACTIVE branch without being in that branch's
    // execution context, which widens their reach instead of restoring it.
    if (!await administratorAuthority.IsTenantAdministratorAsync(
      tenantId, session.TenantUserId, cancellationToken))
    {
      return Result.Failure<BranchTransferDeclaration?>(BranchErrors.TransferNotPermitted);
    }

    var sourceIsInactive = await SourceBranchIsInactiveAsync(
      tenantId, declaration.SourceBranchId, cancellationToken);
    if (sourceIsInactive.IsFailure)
    {
      return Result.Failure<BranchTransferDeclaration?>(sourceIsInactive.Error);
    }

    return sourceIsInactive.Value
      ? Result.Success<BranchTransferDeclaration?>(declaration)
      : Result.Failure<BranchTransferDeclaration?>(BranchErrors.TransferNotPermitted);
  }

  // EXISTS, BELONGS TO THIS TENANT, AND IS NOT ACTIVE. A source that does not exist is not a recovery
  // candidate either — recovery restores reach to a branch that was retired, not to one that never was.
  private async Task<Result<bool>> SourceBranchIsInactiveAsync(
    Guid tenantId,
    Guid sourceBranchId,
    CancellationToken cancellationToken)
  {
    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      // Tenant storage being unreachable is not "the source is inactive". Failing closed with the storage
      // error keeps an outage from being read as a recovery precondition.
      return Result.Failure<bool>(context.Error);
    }

    await using var tenant = context.Value;

    // The tenant global query filter already restricts this to the routed tenant; TenantId is compared
    // explicitly as well so the predicate states the invariant it depends on rather than inheriting it.
    var isInactive = await tenant.Branches
      .AsNoTracking()
      .AnyAsync(
        branch => branch.Id == sourceBranchId && branch.TenantId == tenantId && !branch.IsActive,
        cancellationToken);

    return Result.Success(isInactive);
  }
}
