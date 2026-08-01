using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Events;

public sealed record RefreshTokenReuseDetected : DomainEvent
{
  public RefreshTokenReuseDetected(Guid eventId, DateTimeOffset occurredUtc, long authenticationSessionId, long refreshTokenRecordId, Guid tenantId, string clientId)
    : base(eventId, occurredUtc)
  {
    AuthenticationSessionId = authenticationSessionId;
    RefreshTokenRecordId = refreshTokenRecordId;
    TenantId = tenantId;
    ClientId = clientId;
  }

  public long AuthenticationSessionId { get; }
  public long RefreshTokenRecordId { get; }
  public Guid TenantId { get; }
  public string ClientId { get; }
}
