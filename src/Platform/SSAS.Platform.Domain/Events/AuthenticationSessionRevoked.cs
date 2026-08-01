using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationSessionRevoked : DomainEvent
{
  public AuthenticationSessionRevoked(Guid eventId, DateTimeOffset occurredUtc, long authenticationSessionId, long identityId, Guid tenantId, string clientId, AuthenticationSessionRevocationReason reason)
    : base(eventId, occurredUtc)
  {
    AuthenticationSessionId = authenticationSessionId;
    IdentityId = identityId;
    TenantId = tenantId;
    ClientId = clientId;
    Reason = reason;
  }

  public long AuthenticationSessionId { get; }
  public long IdentityId { get; }
  public Guid TenantId { get; }
  public string ClientId { get; }
  public AuthenticationSessionRevocationReason Reason { get; }
}
