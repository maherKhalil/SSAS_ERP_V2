using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Events;

namespace SSAS.Platform.Domain.Authentication;

public sealed class TenantSelectionTransaction : AggregateRoot<long>, IAuditableEntity
{
  private byte[] secretHash = [];

  private TenantSelectionTransaction(
    long id,
    Guid publicId,
    long identityId,
    string clientId,
    long securityVersionAtAuthentication,
    byte[] secretHash,
    DateTimeOffset createdUtc,
    DateTimeOffset expiresUtc,
    Guid eventId)
    : base(id)
  {
    if (publicId == Guid.Empty || identityId <= 0 || securityVersionAtAuthentication <= 0 || secretHash.Length != 32 ||
      !IsValidClientId(clientId) || expiresUtc <= createdUtc)
    {
      throw new ArgumentException("The tenant-selection transaction values are invalid.");
    }

    PublicId = publicId;
    IdentityId = identityId;
    ClientId = clientId;
    SecurityVersionAtAuthentication = securityVersionAtAuthentication;
    this.secretHash = secretHash.ToArray();
    CreatedUtc = createdUtc.ToUniversalTime();
    ExpiresUtc = expiresUtc.ToUniversalTime();
    RaiseDomainEvent(new TenantSelectionRequired(eventId, CreatedUtc, PublicId, IdentityId, ClientId));
  }

  private TenantSelectionTransaction()
    : base(0)
  {
    ClientId = string.Empty;
  }

  public Guid PublicId { get; private set; }
  public long IdentityId { get; private set; }
  public string ClientId { get; private set; }
  public long SecurityVersionAtAuthentication { get; private set; }
  public DateTimeOffset CreatedUtc { get; private set; }
  public DateTimeOffset ExpiresUtc { get; private set; }
  public DateTimeOffset? ConsumedUtc { get; private set; }
  public DateTimeOffset? RevokedUtc { get; private set; }
  public byte[] RowVersion { get; private set; } = [];
  public DateTimeOffset ModifiedUtc { get; private set; }
  public string? CreatedBy { get; private set; }
  public string? ModifiedBy { get; private set; }
  internal ReadOnlySpan<byte> SecretHash => secretHash;

  public static TenantSelectionTransaction Create(
    Guid publicId,
    long identityId,
    string clientId,
    long securityVersionAtAuthentication,
    byte[] secretHash,
    DateTimeOffset createdUtc,
    DateTimeOffset expiresUtc,
    Guid eventId) => new(
      0,
      publicId,
      identityId,
      clientId,
      securityVersionAtAuthentication,
      secretHash,
      createdUtc,
      expiresUtc,
      eventId);

  public bool IsActive(DateTimeOffset utcNow) =>
    ConsumedUtc is null && RevokedUtc is null && utcNow.ToUniversalTime() < ExpiresUtc;

  public Result Consume(
    long tenantUserId,
    Guid tenantId,
    long authenticationSessionId,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    var utc = occurredUtc.ToUniversalTime();
    if (!IsActive(utc) || tenantUserId <= 0 || tenantId == Guid.Empty || authenticationSessionId <= 0)
    {
      return Result.Failure(AuthenticationErrors.InvalidTenantSelection);
    }

    ConsumedUtc = utc;
    RaiseDomainEvent(new TenantMembershipSelected(
      eventId,
      utc,
      PublicId,
      IdentityId,
      tenantUserId,
      tenantId,
      authenticationSessionId,
      ClientId));
    return Result.Success();
  }

  public Result Revoke(DateTimeOffset occurredUtc)
  {
    if (ConsumedUtc.HasValue || RevokedUtc.HasValue)
    {
      return Result.Failure(AuthenticationErrors.InvalidTenantSelection);
    }

    RevokedUtc = occurredUtc.ToUniversalTime();
    return Result.Success();
  }

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }

  private static bool IsValidClientId(string? clientId) =>
    !string.IsNullOrWhiteSpace(clientId) && clientId.Length <= 64 && string.Equals(clientId, clientId.Trim(), StringComparison.Ordinal);
}
