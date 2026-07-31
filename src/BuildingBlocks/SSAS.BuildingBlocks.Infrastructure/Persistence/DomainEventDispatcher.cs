using System.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

public sealed class DomainEventDispatcher(
  IEnumerable<IDomainEventConsumer> handlers,
  ICorrelationContext correlationContext,
  IRequestMetadata requestMetadata,
  ICurrentUser currentUser) : IDomainEventDispatcher
{
  public async Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(domainEvents);

    var metadata = new DomainEventDispatchMetadata(
      correlationContext.CorrelationId,
      currentUser.UserId,
      requestMetadata.RequestId,
      Activity.Current?.TraceId.ToString());

    foreach (var domainEvent in domainEvents)
    {
      foreach (var handler in handlers)
      {
        cancellationToken.ThrowIfCancellationRequested();
        await handler.HandleAsync(domainEvent, metadata, cancellationToken);
      }
    }
  }
}
