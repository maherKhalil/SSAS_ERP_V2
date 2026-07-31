namespace SSAS.BuildingBlocks.Domain;

public interface IHasDomainEvents
{
  IReadOnlyCollection<DomainEvent> DomainEvents { get; }

  void ClearDomainEvents();
}
