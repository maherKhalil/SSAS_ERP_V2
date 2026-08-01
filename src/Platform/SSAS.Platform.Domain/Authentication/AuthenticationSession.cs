using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;

namespace SSAS.Platform.Domain.Authentication;

public sealed class AuthenticationSession : AggregateRoot<long>, IAuditableEntity
{
  private readonly List<RefreshTokenRecord> refreshTokenRecords = [];

  private AuthenticationSession(
    long id,
    long identityId,
    long tenantUserId,
    Guid tenantId,
    string clientId,
    Guid tokenFamilyId,
    long securityVersionAtCreation,
    DateTimeOffset createdUtc,
    DateTimeOffset idleExpiresUtc,
    DateTimeOffset absoluteExpiresUtc)
    : base(id)
  {
    if (identityId <= 0 || tenantUserId <= 0 || tenantId == Guid.Empty || !IsValidClientId(clientId) ||
      tokenFamilyId == Guid.Empty || securityVersionAtCreation <= 0 || idleExpiresUtc <= createdUtc ||
      absoluteExpiresUtc <= createdUtc || idleExpiresUtc > absoluteExpiresUtc)
    {
      throw new ArgumentException("The authentication-session values are invalid.");
    }

    IdentityId = identityId;
    TenantUserId = tenantUserId;
    TenantId = tenantId;
    ClientId = clientId;
    TokenFamilyId = tokenFamilyId;
    SecurityVersionAtCreation = securityVersionAtCreation;
    Status = AuthenticationSessionStatus.Active;
    CreatedUtc = createdUtc.ToUniversalTime();
    IdleExpiresUtc = idleExpiresUtc.ToUniversalTime();
    AbsoluteExpiresUtc = absoluteExpiresUtc.ToUniversalTime();
  }

  private AuthenticationSession()
    : base(0)
  {
    ClientId = string.Empty;
  }

  public long IdentityId { get; private set; }
  public long TenantUserId { get; private set; }
  public Guid TenantId { get; private set; }
  public string ClientId { get; private set; }
  public Guid TokenFamilyId { get; private set; }
  public AuthenticationSessionStatus Status { get; private set; }
  public DateTimeOffset CreatedUtc { get; private set; }
  public DateTimeOffset? LastRefreshedUtc { get; private set; }
  public DateTimeOffset IdleExpiresUtc { get; private set; }
  public DateTimeOffset AbsoluteExpiresUtc { get; private set; }
  public long SecurityVersionAtCreation { get; private set; }
  public DateTimeOffset? RevokedUtc { get; private set; }
  public string? RevokedBy { get; private set; }
  public AuthenticationSessionRevocationReason? RevocationReason { get; private set; }
  public DateTimeOffset? CompromisedUtc { get; private set; }
  public long? CompromisedByRefreshTokenRecordId { get; private set; }
  public IReadOnlyCollection<RefreshTokenRecord> RefreshTokenRecords => refreshTokenRecords.AsReadOnly();
  public byte[] RowVersion { get; private set; } = [];
  public DateTimeOffset ModifiedUtc { get; private set; }
  public string? CreatedBy { get; private set; }
  public string? ModifiedBy { get; private set; }

  public static AuthenticationSession Create(
    long identityId,
    long tenantUserId,
    Guid tenantId,
    string clientId,
    Guid tokenFamilyId,
    long securityVersionAtCreation,
    DateTimeOffset createdUtc,
    DateTimeOffset idleExpiresUtc,
    DateTimeOffset absoluteExpiresUtc) => new(
      0,
      identityId,
      tenantUserId,
      tenantId,
      clientId,
      tokenFamilyId,
      securityVersionAtCreation,
      createdUtc,
      idleExpiresUtc,
      absoluteExpiresUtc);

  public bool IsUsable(DateTimeOffset utcNow)
  {
    var utc = utcNow.ToUniversalTime();
    return Status == AuthenticationSessionStatus.Active && utc < IdleExpiresUtc && utc < AbsoluteExpiresUtc;
  }

  public RefreshTokenRecord? FindRefreshToken(Guid publicId) =>
    refreshTokenRecords.SingleOrDefault(token => token.PublicId == publicId);

  internal RefreshTokenRecord CreateInitialRefreshToken(
    Guid publicId,
    byte[] secretHash,
    DateTimeOffset createdUtc,
    Guid eventId)
  {
    if (Id <= 0 || refreshTokenRecords.Count != 0 || Status != AuthenticationSessionStatus.Active)
    {
      throw new InvalidOperationException("The initial refresh token cannot be created.");
    }

    var token = RefreshTokenRecord.Create(
      Id,
      publicId,
      secretHash,
      TokenFamilyId,
      ClientId,
      createdUtc,
      Min(IdleExpiresUtc, AbsoluteExpiresUtc));
    refreshTokenRecords.Add(token);
    RaiseDomainEvent(new AuthenticationSessionCreated(
      eventId,
      createdUtc,
      Id,
      IdentityId,
      TenantUserId,
      TenantId,
      ClientId));
    return token;
  }

  internal Result<RefreshTokenRecord> Rotate(
    RefreshTokenRecord predecessor,
    Guid replacementPublicId,
    byte[] replacementSecretHash,
    DateTimeOffset occurredUtc,
    TimeSpan idleLifetime,
    Guid eventId)
  {
    ArgumentNullException.ThrowIfNull(predecessor);
    var utc = occurredUtc.ToUniversalTime();
    if (!IsUsable(utc) || idleLifetime <= TimeSpan.Zero || predecessor.AuthenticationSessionId != Id ||
      predecessor.TokenFamilyId != TokenFamilyId || !string.Equals(predecessor.ClientId, ClientId, StringComparison.Ordinal))
    {
      return Result.Failure<RefreshTokenRecord>(AuthenticationErrors.InvalidRefreshToken);
    }

    var consume = predecessor.Consume(utc);
    if (consume.IsFailure)
    {
      return Result.Failure<RefreshTokenRecord>(consume.Error);
    }

    LastRefreshedUtc = utc;
    IdleExpiresUtc = Min(utc.Add(idleLifetime), AbsoluteExpiresUtc);
    var replacement = RefreshTokenRecord.Create(
      Id,
      replacementPublicId,
      replacementSecretHash,
      TokenFamilyId,
      ClientId,
      utc,
      Min(IdleExpiresUtc, AbsoluteExpiresUtc));
    predecessor.LinkReplacement(replacement);
    refreshTokenRecords.Add(replacement);
    RaiseDomainEvent(new AuthenticationSessionRefreshed(eventId, utc, Id, TenantId, ClientId));
    return Result.Success(replacement);
  }

  public Result Revoke(
    AuthenticationSessionRevocationReason reason,
    string? actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != AuthenticationSessionStatus.Active || !Enum.IsDefined(reason))
    {
      return Result.Failure(AuthenticationErrors.InvalidAuthenticationSession);
    }

    var utc = occurredUtc.ToUniversalTime();
    Status = AuthenticationSessionStatus.Revoked;
    RevokedUtc = utc;
    RevokedBy = string.IsNullOrWhiteSpace(actor) ? null : actor;
    RevocationReason = reason;
    foreach (var token in refreshTokenRecords)
    {
      token.Revoke(utc);
    }

    RaiseDomainEvent(new AuthenticationSessionRevoked(eventId, utc, Id, IdentityId, TenantId, ClientId, reason));
    if (reason == AuthenticationSessionRevocationReason.SessionLimitExceeded)
    {
      RaiseDomainEvent(new SessionLimitOldestSessionRevoked(Guid.NewGuid(), utc, Id, IdentityId, TenantId, ClientId));
    }

    return Result.Success();
  }

  internal Result MarkCompromised(
    RefreshTokenRecord triggeringToken,
    Guid compromiseEventId,
    Guid reuseEventId,
    DateTimeOffset occurredUtc)
  {
    ArgumentNullException.ThrowIfNull(triggeringToken);
    if (Status != AuthenticationSessionStatus.Active || triggeringToken.AuthenticationSessionId != Id ||
      triggeringToken.ConsumedUtc is null || triggeringToken.Id <= 0)
    {
      return Result.Failure(AuthenticationErrors.InvalidRefreshToken);
    }

    var utc = occurredUtc.ToUniversalTime();
    Status = AuthenticationSessionStatus.Compromised;
    CompromisedUtc = utc;
    CompromisedByRefreshTokenRecordId = triggeringToken.Id;
    RevokeDescendants(triggeringToken, utc);
    RaiseDomainEvent(new AuthenticationSessionCompromised(
      compromiseEventId,
      utc,
      Id,
      IdentityId,
      TenantId,
      ClientId,
      triggeringToken.Id));
    RaiseDomainEvent(new RefreshTokenReuseDetected(
      reuseEventId,
      utc,
      Id,
      triggeringToken.Id,
      TenantId,
      ClientId));
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

  private void RevokeDescendants(RefreshTokenRecord ancestor, DateTimeOffset occurredUtc)
  {
    var nextId = ancestor.ReplacedByRefreshTokenRecordId;
    var next = ancestor.ReplacedByRefreshTokenRecord;
    while (next is not null || nextId.HasValue)
    {
      next ??= refreshTokenRecords.SingleOrDefault(token => token.Id == nextId);
      if (next is null)
      {
        break;
      }

      next.Revoke(occurredUtc);
      nextId = next.ReplacedByRefreshTokenRecordId;
      next = next.ReplacedByRefreshTokenRecord;
    }
  }

  private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

  private static bool IsValidClientId(string? clientId) =>
    !string.IsNullOrWhiteSpace(clientId) && clientId.Length <= 64 && string.Equals(clientId, clientId.Trim(), StringComparison.Ordinal);
}
