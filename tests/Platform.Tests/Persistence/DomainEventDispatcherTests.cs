using System.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.Persistence;

public sealed class DomainEventDispatcherTests
{
  [Fact]
  public async Task Dispatch_attaches_request_metadata_outside_the_domain_event()
  {
    var consumer = new RecordingConsumer();
    var dispatcher = new DomainEventDispatcher(
      [consumer],
      new TestCorrelationContext(),
      new TestRequestMetadata(),
      new TestCurrentUser());
    using var activity = new Activity("domain-event-test").Start();
    var domainEvent = new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);

    await dispatcher.DispatchAsync([domainEvent]);

    Assert.Same(domainEvent, consumer.DomainEvent);
    Assert.Equal("correlation-123", consumer.Metadata?.CorrelationId);
    Assert.Equal("actor-456", consumer.Metadata?.ActorId);
    Assert.Equal("request-789", consumer.Metadata?.RequestId);
    Assert.Equal(activity.TraceId.ToString(), consumer.Metadata?.TraceId);
    Assert.DoesNotContain(
      typeof(TestDomainEvent).GetProperties(),
      property => property.Name.Contains("Correlation", StringComparison.Ordinal));
  }

  private sealed class RecordingConsumer : IDomainEventConsumer
  {
    public DomainEvent? DomainEvent { get; private set; }

    public DomainEventDispatchMetadata? Metadata { get; private set; }

    public Task HandleAsync(
      DomainEvent domainEvent,
      DomainEventDispatchMetadata metadata,
      CancellationToken cancellationToken = default)
    {
      DomainEvent = domainEvent;
      Metadata = metadata;
      return Task.CompletedTask;
    }
  }

  private sealed class TestCorrelationContext : ICorrelationContext
  {
    public string CorrelationId => "correlation-123";
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "actor-456";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestRequestMetadata : IRequestMetadata
  {
    public string? RequestId => "request-789";
  }

  private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredUtc)
    : DomainEvent(EventId, OccurredUtc);
}
