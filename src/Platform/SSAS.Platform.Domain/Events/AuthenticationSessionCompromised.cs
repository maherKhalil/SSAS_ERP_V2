using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record AuthenticationSessionCompromised : DomainEvent
{
  public AuthenticationSessionCompromised(Guid eventId, DateTimeOffset occurredUtc, long authenticationSessionId, long identityId, Guid tenantId, string clientId, long triggeringRefreshTokenRecordId)
    : base(eventId, occurredUtc)
  {
    AuthenticationSessionId = authenticationSessionId;
    IdentityId = identityId;
    TenantId = tenantId;
    ClientId = clientId;
    TriggeringRefreshTokenRecordId = triggeringRefreshTokenRecordId;
  }

  public long AuthenticationSessionId { get; }
  public long IdentityId { get; }
  public Guid TenantId { get; }
  public string ClientId { get; }
  public long TriggeringRefreshTokenRecordId { get; }
}
