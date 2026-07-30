namespace SSAS.BuildingBlocks.Domain;

public abstract class AggregateRoot<TId> : Entity<TId>
  where TId : notnull
{
  private readonly List<DomainEvent> domainEvents = [];

  protected AggregateRoot(TId id)
    : base(id)
  {
  }

  public IReadOnlyCollection<DomainEvent> DomainEvents => domainEvents.AsReadOnly();

  protected void RaiseDomainEvent(DomainEvent domainEvent)
  {
    ArgumentNullException.ThrowIfNull(domainEvent);

    domainEvents.Add(domainEvent);
  }

  public IReadOnlyCollection<DomainEvent> DequeueDomainEvents()
  {
    var events = domainEvents.ToArray();
    domainEvents.Clear();

    return events;
  }
}
