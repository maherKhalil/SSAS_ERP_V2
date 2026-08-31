using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

// ==================================================================================================
// DISPATCH RUNS AFTER A DURABLE COMMIT, SO IT MUST NOT THROW (item 173).
// ==================================================================================================
//
// `EfUnitOfWork` calls this once the transaction has committed. **By then the write cannot be undone**,
// so anything thrown from here would reach the caller as a failed command over data that was in fact
// written -- and a caller told "failed" may retry an operation that already happened.
//
// ---- ⚠ SURFACED, NOT SWALLOWED.
//
// A consumer failure is logged at ERROR with its correlation id, event type and consumer type. **The one
// registered consumer invalidates an entitlement cache, and a silent failure there is a stale cache
// nobody sees** -- so the log is the whole of the signal and it names what failed rather than that
// something did.
//
// ---- ⚠ CONSUMERS ARE ISOLATED FROM EACH OTHER.
//
// The loop used to abandon the remainder on the first throw, so one bad consumer stopped every other from
// seeing any event. Each is now attempted independently.
//
// ---- ⚠ WHY CANCELLATION STOPS THE LOOP WITHOUT THROWING.
//
// The previous `ThrowIfCancellationRequested` would, from this position, report a committed command as
// cancelled -- the same defect wearing a different exception. A cancelled dispatch stops and is logged;
// the command still succeeded, because it did.
public sealed class DomainEventDispatcher(
  IEnumerable<IDomainEventConsumer> handlers,
  ICorrelationContext correlationContext,
  IRequestMetadata requestMetadata,
  ICurrentUser currentUser,
  ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
  private static readonly Action<ILogger, string, string, string, Exception?> LogConsumerFailure =
    LoggerMessage.Define<string, string, string>(
      LogLevel.Error,
      new EventId(1, nameof(LogConsumerFailure)),
      "Domain event consumer {Consumer} failed handling {DomainEvent} after commit (correlation {CorrelationId}). " +
      "The command succeeded and the write is durable; whatever this consumer maintains is now stale.");

  private static readonly Action<ILogger, string, Exception?> LogDispatchCancelled =
    LoggerMessage.Define<string>(
      LogLevel.Warning,
      new EventId(2, nameof(LogDispatchCancelled)),
      "Domain event dispatch was cancelled after commit (correlation {CorrelationId}). " +
      "The command succeeded; consumers after this point did not run.");

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
        if (cancellationToken.IsCancellationRequested)
        {
          LogDispatchCancelled(logger, metadata.CorrelationId, null);

          return;
        }

        try
        {
          await handler.HandleAsync(domainEvent, metadata, cancellationToken);
        }
        catch (Exception exception)
        {
          LogConsumerFailure(
            logger,
            handler.GetType().Name,
            domainEvent.GetType().Name,
            metadata.CorrelationId,
            exception);
        }
      }
    }
  }
}
