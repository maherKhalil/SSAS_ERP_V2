using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ITenantSelectionTransactionRepository
{
  Task<long?> GetIdentityIdByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

  Task<TenantSelectionTransaction?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken cancellationToken = default);

  Task AddAsync(TenantSelectionTransaction transaction, CancellationToken cancellationToken = default);
}
