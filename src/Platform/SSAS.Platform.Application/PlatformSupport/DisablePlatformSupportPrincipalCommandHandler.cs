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

    var principal = await principalRepository.GetByIdAsync(command.PlatformSupportPrincipalId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure(PlatformSupportErrors.PrincipalNotFound);
    }

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
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
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
