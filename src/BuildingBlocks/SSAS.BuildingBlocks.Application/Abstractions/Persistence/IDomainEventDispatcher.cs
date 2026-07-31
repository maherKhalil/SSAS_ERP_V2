using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Application.Abstractions.Persistence;

public interface IDomainEventDispatcher
{
  Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
