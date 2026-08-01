using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record SessionLimitOldestSessionRevoked : DomainEvent
{
  public SessionLimitOldestSessionRevoked(Guid eventId, DateTimeOffset occurredUtc, long authenticationSessionId, long identityId, Guid tenantId, string clientId)
    : base(eventId, occurredUtc)
  {
    AuthenticationSessionId = authenticationSessionId;
    IdentityId = identityId;
    TenantId = tenantId;
    ClientId = clientId;
  }

  public long AuthenticationSessionId { get; }
  public long IdentityId { get; }
  public Guid TenantId { get; }
  public string ClientId { get; }
}
