namespace SSAS.BuildingBlocks.Domain;

public abstract record DomainEvent
{
  protected DomainEvent(Guid eventId, DateTimeOffset occurredAt, string correlationId)
  {
    if (eventId == Guid.Empty)
    {
      throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));
    }

    if (string.IsNullOrWhiteSpace(correlationId))
    {
      throw new ArgumentException("Correlation ID cannot be null or whitespace.", nameof(correlationId));
    }

    EventId = eventId;
    OccurredAt = occurredAt;
    CorrelationId = correlationId;
  }

  public Guid EventId { get; }

  public DateTimeOffset OccurredAt { get; }

  public string CorrelationId { get; }
}
