using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.PlatformSupport;

public sealed class DisablePlatformSupportPrincipalCommandHandler(
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformAuthenticationSessionRepository sessionRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DisablePlatformSupportPrincipalCommand command,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure(actor.Error);
    }

    // GLOBAL LOCK ORDER (DEC-TEN-0023 / L1): principal → session(s), matching creation's account → principal →
    // session. The principal is read under an update lock INSIDE this transaction and held to commit, so a
    // concurrent platform-session creation serializes on the same row: it either observes Disabled and fails, or
    // commits first and has its session revoked by the range read below.
    //
    // LOAD-BEARING INVARIANT: take the principal UPDLOCK first, and do not promote it to an exclusive lock before
    // the session range below has been acquired. The status change is applied in memory here, but the principal
    // UPDATE (U → X) is only flushed by SaveChanges AFTER the session-range read, which is what keeps this safe:
    // refresh may hold a session lock and then read this principal ordinarily, and that read is compatible with a
    // U lock (S/U do not conflict) so it completes. Were the principal written to the database before the session
    // range were taken, that read would meet an X lock and the pair (refresh: session → principal,
    // disable: principal → session) would form a cycle. Do not move the save above the session-range read.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var principal = await principalRepository.GetByIdForUpdateAsync(command.PlatformSupportPrincipalId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure(PlatformSupportErrors.PrincipalNotFound);
    }

    // Optimistic concurrency is unchanged: the caller's expected RowVersion is still validated, now against the
    // lock-protected read, and the UPDATE below still carries EF's RowVersion concurrency token.
    if (!ApplicationExecutionContext.MatchesExpectedVersion(principal.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var now = clock.UtcNow;
    var domainResult = principal.Disable(actor.Value, now);
    if (domainResult.IsFailure)
    {
      return domainResult;
    }

    // Proactive revocation (ADR-016 / DEC-TEN-0022, F3C-3): revoke ALL active platform sessions for this
    // principal in the SAME transaction as the status change, so no active platform session survives the
    // Disable. This never touches AuthenticationAccount.SecurityVersion (F3C-1) or tenant sessions; the
    // refresh-time live status check remains an independent fail-closed backstop.
    var activeSessions = await sessionRepository.ListActiveByPrincipalForUpdateAsync(principal.Id, cancellationToken);
    foreach (var session in activeSessions)
    {
      var revoked = session.Revoke(PlatformAuthenticationSessionRevocationReason.PlatformPrincipalIneligible, actor.Value, now);
      if (revoked.IsFailure)
      {
        return revoked;
      }
    }

    var save = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (save.IsFailure)
    {
      return Result.Failure(save.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success();
  }
}
