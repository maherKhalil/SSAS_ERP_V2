using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

// Revokes the caller's current platform authentication session (Phase 4B / DEC-TEN-0023). Platform store only:
// it loads the session by the trusted session_id under an update lock, verifies it structurally belongs to the
// authenticated identity, and revokes it with UserLogout. It never touches the tenant session store, never
// changes AuthenticationAccount.SecurityVersion, and never invalidates the already-issued short-lived access JWT
// (which remains valid until natural expiry) — logout only stops refresh continuation. It is idempotent and
// fail-closed: an already-revoked, missing, or non-owned session is a no-op success with no state disclosure.
public sealed class RevokeCurrentPlatformAuthenticationSessionCommandHandler(
  IPlatformAuthenticationSessionRepository sessionRepository,
  IPlatformUnitOfWork unitOfWork,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    RevokeCurrentPlatformAuthenticationSessionCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var session = await sessionRepository.GetByIdForUpdateAsync(command.PlatformAuthenticationSessionId, cancellationToken);

    // Fail-closed idempotency: only an Active session that belongs to the authenticated identity is revoked.
    // Anything else (missing, already revoked/compromised, or another identity's session) is a silent no-op —
    // no external distinction is exposed, and a foreign session_id can never be revoked.
    if (session is null || session.IdentityId != command.IdentityId ||
      session.Status != AuthenticationSessionStatus.Active)
    {
      return Result.Success();
    }

    var revoked = session.Revoke(PlatformAuthenticationSessionRevocationReason.UserLogout, null, clock.UtcNow);
    if (revoked.IsFailure)
    {
      return Result.Success();
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
