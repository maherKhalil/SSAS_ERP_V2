using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Application.Abstractions.Persistence;

public interface IDomainEventConsumer
{
  Task HandleAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}
