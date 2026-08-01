using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class AccountActionTokenRepository(PlatformDbContext dbContext) : IAccountActionTokenRepository
{
  public Task<AccountActionToken?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
    dbContext.AccountActionTokens.SingleOrDefaultAsync(token => token.PublicId == publicId, cancellationToken);

  public Task<AccountActionToken?> GetActiveInvitationAsync(
    Guid tenantId,
    long tenantUserId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default) => dbContext.AccountActionTokens.SingleOrDefaultAsync(
      token => token.Purpose == AccountActionTokenPurpose.Invitation &&
        token.TenantId == tenantId &&
        token.TenantUserId == tenantUserId &&
        token.ConsumedUtc == null &&
        token.RevokedUtc == null,
      cancellationToken);

  public Task<AccountActionToken?> GetActivePasswordResetAsync(
    long authenticationAccountId,
    DateTimeOffset utcNow,
    CancellationToken cancellationToken = default) => dbContext.AccountActionTokens.SingleOrDefaultAsync(
      token => token.Purpose == AccountActionTokenPurpose.PasswordReset &&
        token.AuthenticationAccountId == authenticationAccountId &&
        token.ConsumedUtc == null &&
        token.RevokedUtc == null,
      cancellationToken);

  public async Task AddAsync(AccountActionToken actionToken, CancellationToken cancellationToken = default)
  {
    await dbContext.AccountActionTokens.AddAsync(actionToken, cancellationToken);
  }
}
