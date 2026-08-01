using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record TenantMembershipSelected : DomainEvent
{
  public TenantMembershipSelected(Guid eventId, DateTimeOffset occurredUtc, Guid transactionPublicId, long identityId, long tenantUserId, Guid tenantId, long authenticationSessionId, string clientId)
    : base(eventId, occurredUtc)
  {
    TransactionPublicId = transactionPublicId;
    IdentityId = identityId;
    TenantUserId = tenantUserId;
    TenantId = tenantId;
    AuthenticationSessionId = authenticationSessionId;
    ClientId = clientId;
  }

  public Guid TransactionPublicId { get; }
  public long IdentityId { get; }
  public long TenantUserId { get; }
  public Guid TenantId { get; }
  public long AuthenticationSessionId { get; }
  public string ClientId { get; }
}
