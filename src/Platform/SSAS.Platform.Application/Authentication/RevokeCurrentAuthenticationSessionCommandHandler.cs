using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

public sealed class RevokeCurrentAuthenticationSessionCommandHandler(
  ICurrentAuthenticationSession currentSession,
  IAuthenticationAccountRepository accountRepository,
  IAuthenticationSessionRepository sessionRepository,
  IPlatformUnitOfWork unitOfWork,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    RevokeCurrentAuthenticationSessionCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    var current = currentSession.Value;
    if (current is null)
    {
      return Result.Failure(AuthenticationErrors.InvalidAuthenticationSession);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(current.IdentityId, cancellationToken);
    if (account is null)
    {
      return Result.Success();
    }

    var session = await sessionRepository.GetByIdForUpdateAsync(current.AuthenticationSessionId, cancellationToken);
    if (session is null || session.Status != AuthenticationSessionStatus.Active)
    {
      return Result.Success();
    }

    if (account.IdentityId != current.IdentityId ||
      account.SecurityVersion != current.SecurityVersion ||
      session.IdentityId != current.IdentityId ||
      session.TenantId != current.TenantId ||
      session.TenantUserId != current.TenantUserId ||
      session.SecurityVersionAtCreation != current.SecurityVersion ||
      !string.Equals(session.ClientId, current.ClientId.Value, StringComparison.Ordinal))
    {
      return Result.Failure(AuthenticationErrors.InvalidAuthenticationSession);
    }

    var revoked = session.Revoke(
      AuthenticationSessionRevocationReason.UserLogout,
      null,
      Guid.NewGuid(),
      clock.UtcNow);
    if (revoked.IsFailure)
    {
      return revoked;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success();
  }
}
