using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantSelectionRequired : DomainEvent
{
  public TenantSelectionRequired(Guid eventId, DateTimeOffset occurredUtc, Guid transactionPublicId, long identityId, string clientId)
    : base(eventId, occurredUtc)
  {
    TransactionPublicId = transactionPublicId;
    IdentityId = identityId;
    ClientId = clientId;
  }

  public Guid TransactionPublicId { get; }
  public long IdentityId { get; }
  public string ClientId { get; }
}
