using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Tests.BuildingBlocks;

public sealed class AggregateRootTests
{
  [Fact]
  public void Dequeue_domain_events_returns_and_clears_raised_events()
  {
    var aggregate = new TestAggregate(Guid.NewGuid());
    var domainEvent = new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UnixEpoch);

    aggregate.Record(domainEvent);

    var dequeuedEvents = aggregate.DequeueDomainEvents();

    Assert.Equal([domainEvent], dequeuedEvents);
    Assert.Empty(aggregate.DomainEvents);
  }

  private sealed class TestAggregate(Guid id) : AggregateRoot<Guid>(id)
  {
    public void Record(DomainEvent domainEvent)
    {
      RaiseDomainEvent(domainEvent);
    }
  }

  private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredUtc)
    : DomainEvent(EventId, OccurredUtc);
}
