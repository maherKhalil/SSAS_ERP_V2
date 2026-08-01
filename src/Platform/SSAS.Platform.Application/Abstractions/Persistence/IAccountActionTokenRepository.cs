using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IAccountActionTokenRepository
{
  Task<AccountActionToken?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

  Task<AccountActionToken?> GetActiveInvitationAsync(
    Guid tenantId,
    long tenantUserId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default);

  Task<AccountActionToken?> GetActivePasswordResetAsync(
    long authenticationAccountId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default);

  Task AddAsync(AccountActionToken actionToken, CancellationToken cancellationToken = default);
}
