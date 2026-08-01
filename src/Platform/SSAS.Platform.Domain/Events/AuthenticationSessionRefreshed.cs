using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationSessionRefreshed : DomainEvent
{
  public AuthenticationSessionRefreshed(Guid eventId, DateTimeOffset occurredUtc, long authenticationSessionId, Guid tenantId, string clientId)
    : base(eventId, occurredUtc)
  {
    AuthenticationSessionId = authenticationSessionId;
    TenantId = tenantId;
    ClientId = clientId;
  }

  public long AuthenticationSessionId { get; }
  public Guid TenantId { get; }
  public string ClientId { get; }
}
