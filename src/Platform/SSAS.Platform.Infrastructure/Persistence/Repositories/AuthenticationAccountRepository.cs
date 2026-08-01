using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class AuthenticationAccountRepository(PlatformDbContext dbContext) : IAuthenticationAccountRepository
{
  public Task<AuthenticationAccount?> GetByIdAsync(
    long authenticationAccountId,
    CancellationToken cancellationToken = default) => dbContext.AuthenticationAccounts
      .SingleOrDefaultAsync(account => account.Id == authenticationAccountId, cancellationToken);

  public Task<AuthenticationAccount?> GetByIdentityIdAsync(
    long identityId,
    CancellationToken cancellationToken = default) => dbContext.AuthenticationAccounts
      .SingleOrDefaultAsync(account => account.IdentityId == identityId, cancellationToken);

  public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(
    string normalizedLoginEmail,
    CancellationToken cancellationToken = default) => dbContext.AuthenticationAccounts
      .SingleOrDefaultAsync(account => account.NormalizedLoginEmail == normalizedLoginEmail, cancellationToken);

  public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
    dbContext.Entry(account).ReloadAsync(cancellationToken);

  public async Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default)
  {
    await dbContext.AuthenticationAccounts.AddAsync(account, cancellationToken);
  }
}
