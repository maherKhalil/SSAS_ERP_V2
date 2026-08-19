using SSAS.BuildingBlocks.Tenancy.Branches;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Branches;

// Decides and records which branch a session works in (Branch foundation B1c).
internal sealed class BranchSessionService(
  PlatformDbContext platform,
  ITenantBranchAccessResolver accessResolver,
  ITenantAdministratorAuthority administratorAuthority,
  IDateTimeProvider clock) : IBranchSessionService
{
  public async Task<Result<BranchSessionState>> ResolveForSessionAsync(
    long authenticationSessionId,
    CancellationToken cancellationToken = default)
  {
    var session = await LoadUsableSessionAsync(authenticationSessionId, cancellationToken);
    if (session is null)
    {
      return Result.Failure<BranchSessionState>(BranchErrors.ContextRequired);
    }

    var permitted = await accessResolver.GetPermittedBranchesAsync(
      session.TenantId, session.TenantUserId, cancellationToken);
    if (permitted.IsFailure)
    {
      return Result.Failure<BranchSessionState>(permitted.Error);
    }

    var branches = permitted.Value;

    if (branches.Count == 0)
    {
      // ---- ZERO MEANS TWO DIFFERENT THINGS, and confusing them is the failure worth avoiding.
      //
      // For an ADMINISTRATOR it is onboarding: the tenant has no branches yet and they are the one who
      // creates the first. For a NORMAL USER it is an account that cannot work anywhere — which B1b's
      // invariant says should be unreachable — so it fails closed rather than presenting an empty picker
      // or, worse, treating "no restrictions found" as "no restrictions".
      return await administratorAuthority.IsTenantAdministratorAsync(
        session.TenantId, session.TenantUserId, cancellationToken)
        ? Result.Success(new BranchSessionState(BranchSessionOutcome.FirstBranchRequired, null, []))
        : Result.Failure<BranchSessionState>(BranchErrors.AccountIntegrityFailure);
    }

    if (branches.Count > 1)
    {
      // The session stays branch-less on purpose. Picking one for the user would silently decide where
      // their work is recorded.
      return Result.Success(
        new BranchSessionState(BranchSessionOutcome.BranchSelectionRequired, null, branches));
    }

    // ---- EXACTLY ONE: chosen for them, because there is nothing to choose.
    var only = branches[0].BranchId;
    var selected = session.SelectBranch(only);
    if (selected.IsFailure)
    {
      return Result.Failure<BranchSessionState>(selected.Error);
    }

    await platform.SaveChangesAsync(cancellationToken);
    return Result.Success(new BranchSessionState(BranchSessionOutcome.Active, only, branches));
  }

  public async Task<Result<BranchSessionState>> SelectActiveBranchAsync(
    long authenticationSessionId,
    Guid branchId,
    CancellationToken cancellationToken = default)
  {
    if (branchId == Guid.Empty)
    {
      return Result.Failure<BranchSessionState>(BranchErrors.InvalidSelection);
    }

    var session = await LoadUsableSessionAsync(authenticationSessionId, cancellationToken);
    if (session is null)
    {
      return Result.Failure<BranchSessionState>(BranchErrors.ContextRequired);
    }

    // ---- REVALIDATED NOW, NOT AGAINST THE LIST RETURNED AT LOGIN. That list may be minutes old; access
    // can be revoked and a branch deactivated in between, and switching is exactly when a user acts on a
    // stale picker.
    var authorized = await accessResolver.AuthorizeBranchAsync(
      session.TenantId, session.TenantUserId, branchId, cancellationToken);
    if (authorized.IsFailure)
    {
      // THE CURRENT BRANCH IS LEFT ALONE. A refused switch must not strand the user out of the branch they
      // were legitimately working in.
      return Result.Failure<BranchSessionState>(authorized.Error);
    }

    // Selecting the branch already active is a no-op that succeeds: a client retrying or re-confirming is
    // not an error, and refusing would make idempotent navigation fail.
    var selected = session.SelectBranch(branchId);
    if (selected.IsFailure)
    {
      return Result.Failure<BranchSessionState>(selected.Error);
    }

    try
    {
      await platform.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
      // Two switches on one session raced. Reported rather than last-write-wins, so a client learns its
      // view of the session is stale instead of silently landing somewhere it did not choose.
      return Result.Failure<BranchSessionState>(BranchErrors.ConcurrencyConflict);
    }

    return Result.Success(new BranchSessionState(BranchSessionOutcome.Active, branchId, []));
  }

  // TRACKED, because both callers may write to it — and re-read every time rather than cached, so a
  // session revoked since the token was issued cannot acquire or change branch context.
  private async Task<AuthenticationSession?> LoadUsableSessionAsync(
    long authenticationSessionId,
    CancellationToken cancellationToken)
  {
    if (authenticationSessionId <= 0)
    {
      return null;
    }

    var session = await platform.Set<AuthenticationSession>()
      .SingleOrDefaultAsync(candidate => candidate.Id == authenticationSessionId, cancellationToken);

    return session is null || session.Status != AuthenticationSessionStatus.Active ||
      !session.IsUsable(clock.UtcNow)
      ? null
      : session;
  }
}
