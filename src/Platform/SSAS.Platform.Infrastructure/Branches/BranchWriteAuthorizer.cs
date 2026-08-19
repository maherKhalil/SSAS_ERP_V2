using SSAS.BuildingBlocks.Tenancy.Branches;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Branches;

// Resolves and re-authorizes the session's active branch on every branch-owned write (Branch foundation
// B1c).
internal sealed class BranchWriteAuthorizer(
  PlatformDbContext platform,
  ITenantBranchAccessResolver accessResolver,
  IDateTimeProvider clock,
  // OPTIONAL, AND ITS ABSENCE IS A REFUSAL. The current session is a request-scoped, web-layer concept;
  // background and maintenance compositions have none. Registering this as required would make the
  // persistence container un-buildable outside a request — and defaulting to "allow" when it is missing
  // would be far worse, so a null session simply means no branch context and every branch-owned write is
  // refused.
  ICurrentAuthenticationSession? currentSession = null) : IBranchWriteAuthorizer
{
  public async Task<Result<Guid>> AuthorizeCurrentBranchAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || currentSession?.Value is not { } session || session.TenantId != tenantId)
    {
      return Result.Failure<Guid>(BranchErrors.ContextRequired);
    }

    // ---- THE DURABLE SESSION IS THE ONLY SOURCE OF THE ACTIVE BRANCH.
    //
    // Its STATUS and EXPIRY are re-read too: a revoked or expired session must not keep writing through a
    // branch it selected while it was still usable, and the access token it presented may outlive the
    // revocation by up to its (short) lifetime.
    var stored = await platform.Set<Domain.Authentication.AuthenticationSession>()
      .AsNoTracking()
      .Where(candidate => candidate.Id == session.AuthenticationSessionId &&
        candidate.TenantId == tenantId &&
        candidate.TenantUserId == session.TenantUserId)
      .Select(candidate => new
      {
        candidate.ActiveBranchId,
        candidate.Status,
        candidate.IdleExpiresUtc,
        candidate.AbsoluteExpiresUtc
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (stored is null ||
      stored.Status != Domain.Enums.AuthenticationSessionStatus.Active ||
      clock.UtcNow >= stored.IdleExpiresUtc ||
      clock.UtcNow >= stored.AbsoluteExpiresUtc)
    {
      return Result.Failure<Guid>(BranchErrors.ContextRequired);
    }

    // No branch chosen yet. A user authorized for several branches is authenticated but not yet working
    // anywhere, and branch-owned data has nowhere to belong.
    if (stored.ActiveBranchId is not { } branchId)
    {
      return Result.Failure<Guid>(BranchErrors.SelectionRequired);
    }

    // ---- AND THE AUTHORIZATION IS ASKED AGAIN, NOW. Assignment revoked, administrator authority revoked,
    // branch deactivated — each of these leaves the stored branch id perfectly readable and no longer
    // usable, which is precisely why the stored value is not trusted on its own.
    var authorized = await accessResolver.AuthorizeBranchAsync(
      tenantId, session.TenantUserId, branchId, cancellationToken);

    return authorized.IsFailure
      ? Result.Failure<Guid>(authorized.Error)
      : Result.Success(branchId);
  }
}
