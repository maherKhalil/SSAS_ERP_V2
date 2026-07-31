using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

public sealed class DomainEventDispatcher(IEnumerable<IDomainEventConsumer> handlers) : IDomainEventDispatcher
{
  public async Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(domainEvents);

    foreach (var domainEvent in domainEvents)
    {
      foreach (var handler in handlers)
      {
        cancellationToken.ThrowIfCancellationRequested();
        await handler.HandleAsync(domainEvent, cancellationToken);
      }
    }
  }
}
