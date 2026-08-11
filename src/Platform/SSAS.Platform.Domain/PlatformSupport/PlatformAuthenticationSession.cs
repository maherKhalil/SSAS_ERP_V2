using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.PlatformSupport;

// Platform-plane authentication session (ADR-016 Phase 3C / DEC-TEN-0022). A separate aggregate from the
// tenant AuthenticationSession: it is global/non-tenant (NOT ITenantOwnedEntity, NO TenantId/TenantUserId/
// CompanyId), anchored to both the global Identity and the PlatformSupportPrincipal that owns the authority.
// It reuses the global AuthenticationAccount.SecurityVersion (snapshotted at creation) — PlatformSupportPrincipal
// has no SecurityVersion of its own. Session status reuses the generic AuthenticationSessionStatus; revocation
// reasons are the platform-specific set. It raises no domain events (no Phase-3C-3 consumer); the session-
// creation and refresh orchestration is Phase 3C-4.
public sealed class PlatformAuthenticationSession : AggregateRoot<long>, IAuditableEntity
{
  private readonly List<PlatformRefreshTokenRecord> refreshTokenRecords = [];

  private PlatformAuthenticationSession(
    long id,
    long identityId,
    long platformSupportPrincipalId,
    string clientId,
    Guid tokenFamilyId,
    long securityVersionAtCreation,
    DateTimeOffset createdUtc,
    DateTimeOffset idleExpiresUtc,
    DateTimeOffset absoluteExpiresUtc)
    : base(id)
  {
    if (identityId <= 0 || platformSupportPrincipalId <= 0 || !IsValidClientId(clientId) ||
      tokenFamilyId == Guid.Empty || securityVersionAtCreation <= 0 || idleExpiresUtc <= createdUtc ||
      absoluteExpiresUtc <= createdUtc || idleExpiresUtc > absoluteExpiresUtc)
    {
      throw new ArgumentException("The platform authentication-session values are invalid.");
    }

    IdentityId = identityId;
    PlatformSupportPrincipalId = platformSupportPrincipalId;
    ClientId = clientId;
    TokenFamilyId = tokenFamilyId;
    SecurityVersionAtCreation = securityVersionAtCreation;
    Status = AuthenticationSessionStatus.Active;
    CreatedUtc = createdUtc.ToUniversalTime();
    IdleExpiresUtc = idleExpiresUtc.ToUniversalTime();
    AbsoluteExpiresUtc = absoluteExpiresUtc.ToUniversalTime();
  }

  private PlatformAuthenticationSession()
    : base(0)
  {
    ClientId = string.Empty;
  }

  public long IdentityId { get; private set; }
  public long PlatformSupportPrincipalId { get; private set; }
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
  public PlatformAuthenticationSessionRevocationReason? RevocationReason { get; private set; }
  public DateTimeOffset? CompromisedUtc { get; private set; }
  public long? CompromisedByRefreshTokenRecordId { get; private set; }
  public IReadOnlyCollection<PlatformRefreshTokenRecord> RefreshTokenRecords => refreshTokenRecords.AsReadOnly();
  public byte[] RowVersion { get; private set; } = [];
  public DateTimeOffset ModifiedUtc { get; private set; }
  public string? CreatedBy { get; private set; }
  public string? ModifiedBy { get; private set; }

  public static PlatformAuthenticationSession Create(
    long identityId,
    long platformSupportPrincipalId,
    string clientId,
    Guid tokenFamilyId,
    long securityVersionAtCreation,
    DateTimeOffset createdUtc,
    DateTimeOffset idleExpiresUtc,
    DateTimeOffset absoluteExpiresUtc) => new(
      0,
      identityId,
      platformSupportPrincipalId,
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

  public PlatformRefreshTokenRecord? FindRefreshToken(Guid publicId) =>
    refreshTokenRecords.SingleOrDefault(token => token.PublicId == publicId);

  public PlatformRefreshTokenRecord CreateInitialRefreshToken(
    Guid publicId,
    byte[] secretHash,
    DateTimeOffset createdUtc)
  {
    if (Id <= 0 || refreshTokenRecords.Count != 0 || Status != AuthenticationSessionStatus.Active)
    {
      throw new InvalidOperationException("The initial platform refresh token cannot be created.");
    }

    var token = PlatformRefreshTokenRecord.Create(
      Id,
      publicId,
      secretHash,
      TokenFamilyId,
      ClientId,
      createdUtc,
      Min(IdleExpiresUtc, AbsoluteExpiresUtc));
    refreshTokenRecords.Add(token);
    return token;
  }

  public Result<PlatformRefreshTokenRecord> Rotate(
    PlatformRefreshTokenRecord predecessor,
    Guid replacementPublicId,
    byte[] replacementSecretHash,
    DateTimeOffset occurredUtc,
    TimeSpan idleLifetime)
  {
    ArgumentNullException.ThrowIfNull(predecessor);
    var utc = occurredUtc.ToUniversalTime();
    if (!IsUsable(utc) || idleLifetime <= TimeSpan.Zero || predecessor.PlatformAuthenticationSessionId != Id ||
      predecessor.TokenFamilyId != TokenFamilyId || !string.Equals(predecessor.ClientId, ClientId, StringComparison.Ordinal))
    {
      return Result.Failure<PlatformRefreshTokenRecord>(AuthenticationErrors.InvalidRefreshToken);
    }

    var consume = predecessor.Consume(utc);
    if (consume.IsFailure)
    {
      return Result.Failure<PlatformRefreshTokenRecord>(consume.Error);
    }

    LastRefreshedUtc = utc;
    IdleExpiresUtc = Min(utc.Add(idleLifetime), AbsoluteExpiresUtc);
    var replacement = PlatformRefreshTokenRecord.Create(
      Id,
      replacementPublicId,
      replacementSecretHash,
      TokenFamilyId,
      ClientId,
      utc,
      Min(IdleExpiresUtc, AbsoluteExpiresUtc));
    predecessor.LinkReplacement(replacement);
    refreshTokenRecords.Add(replacement);
    return Result.Success(replacement);
  }

  public Result Revoke(
    PlatformAuthenticationSessionRevocationReason reason,
    string? actor,
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

    return Result.Success();
  }

  public Result MarkCompromised(
    PlatformRefreshTokenRecord triggeringToken,
    DateTimeOffset occurredUtc)
  {
    ArgumentNullException.ThrowIfNull(triggeringToken);
    if (Status != AuthenticationSessionStatus.Active || triggeringToken.PlatformAuthenticationSessionId != Id ||
      triggeringToken.ConsumedUtc is null || triggeringToken.Id <= 0)
    {
      return Result.Failure(AuthenticationErrors.InvalidRefreshToken);
    }

    var utc = occurredUtc.ToUniversalTime();
    Status = AuthenticationSessionStatus.Compromised;
    CompromisedUtc = utc;
    CompromisedByRefreshTokenRecordId = triggeringToken.Id;
    RevokeDescendants(triggeringToken, utc);
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

  private void RevokeDescendants(PlatformRefreshTokenRecord ancestor, DateTimeOffset occurredUtc)
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
