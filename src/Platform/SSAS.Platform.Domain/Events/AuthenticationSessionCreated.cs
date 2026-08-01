using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationSessionCreated : DomainEvent
{
  public AuthenticationSessionCreated(Guid eventId, DateTimeOffset occurredUtc, long authenticationSessionId, long identityId, long tenantUserId, Guid tenantId, string clientId)
    : base(eventId, occurredUtc)
  {
    AuthenticationSessionId = authenticationSessionId;
    IdentityId = identityId;
    TenantUserId = tenantUserId;
    TenantId = tenantId;
    ClientId = clientId;
  }

  public long AuthenticationSessionId { get; }
  public long IdentityId { get; }
  public long TenantUserId { get; }
  public Guid TenantId { get; }
  public string ClientId { get; }
}
