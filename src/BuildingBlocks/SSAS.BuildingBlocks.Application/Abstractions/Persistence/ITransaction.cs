namespace SSAS.BuildingBlocks.Application.Abstractions.Persistence;

public interface ITransaction : IAsyncDisposable
{
  Task CommitAsync(CancellationToken cancellationToken = default);

  Task RollbackAsync(CancellationToken cancellationToken = default);
}
