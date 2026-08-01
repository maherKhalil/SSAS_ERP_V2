using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantSelectionTransactionRepository(PlatformDbContext dbContext)
  : ITenantSelectionTransactionRepository
{
  public Task<long?> GetIdentityIdByPublicIdAsync(
    Guid publicId,
    CancellationToken cancellationToken = default) => dbContext.TenantSelectionTransactions
      .AsNoTracking()
      .Where(transaction => transaction.PublicId == publicId)
      .Select(transaction => (long?)transaction.IdentityId)
      .SingleOrDefaultAsync(cancellationToken);

  public Task<TenantSelectionTransaction?> GetByPublicIdForUpdateAsync(
    Guid publicId,
    CancellationToken cancellationToken = default) => dbContext.TenantSelectionTransactions
      .FromSqlInterpolated($"SELECT * FROM [platform].[TenantSelectionTransactions] WITH (UPDLOCK, HOLDLOCK) WHERE [PublicId] = {publicId}")
      .SingleOrDefaultAsync(cancellationToken);

  public async Task AddAsync(TenantSelectionTransaction transaction, CancellationToken cancellationToken = default)
  {
    await dbContext.TenantSelectionTransactions.AddAsync(transaction, cancellationToken);
  }
}
