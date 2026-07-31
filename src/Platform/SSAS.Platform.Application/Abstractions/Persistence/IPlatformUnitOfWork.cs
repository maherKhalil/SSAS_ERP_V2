using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IPlatformUnitOfWork
{
  Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);

  Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
