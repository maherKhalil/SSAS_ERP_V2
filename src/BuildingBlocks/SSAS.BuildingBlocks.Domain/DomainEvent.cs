namespace SSAS.BuildingBlocks.Domain;

public abstract record DomainEvent
{
  protected DomainEvent(Guid eventId, DateTimeOffset occurredUtc)
  {
    if (eventId == Guid.Empty)
    {
      throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));
    }

    EventId = eventId;
    OccurredUtc = occurredUtc.ToUniversalTime();
  }

  public Guid EventId { get; }

  public DateTimeOffset OccurredUtc { get; }
}
