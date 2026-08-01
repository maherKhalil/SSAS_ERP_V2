using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Authentication;

public sealed class RefreshTokenRecord : Entity<long>
{
  private byte[] secretHash = [];
  private RefreshTokenRecord? replacedByRefreshTokenRecord;

  private RefreshTokenRecord(
    long id,
    long authenticationSessionId,
    Guid publicId,
    byte[] secretHash,
    Guid tokenFamilyId,
    string clientId,
    DateTimeOffset createdUtc,
    DateTimeOffset expiresUtc)
    : base(id)
  {
    if (authenticationSessionId <= 0 || publicId == Guid.Empty || secretHash.Length != 32 ||
      tokenFamilyId == Guid.Empty || !IsValidClientId(clientId) || expiresUtc <= createdUtc)
    {
      throw new ArgumentException("The refresh-token record values are invalid.");
    }

    AuthenticationSessionId = authenticationSessionId;
    PublicId = publicId;
    this.secretHash = secretHash.ToArray();
    TokenFamilyId = tokenFamilyId;
    ClientId = clientId;
    CreatedUtc = createdUtc.ToUniversalTime();
    ExpiresUtc = expiresUtc.ToUniversalTime();
  }

  private RefreshTokenRecord()
    : base(0)
  {
    ClientId = string.Empty;
  }

  public long AuthenticationSessionId { get; private set; }
  public Guid PublicId { get; private set; }
  public Guid TokenFamilyId { get; private set; }
  public string ClientId { get; private set; }
  public DateTimeOffset CreatedUtc { get; private set; }
  public DateTimeOffset ExpiresUtc { get; private set; }
  public DateTimeOffset? ConsumedUtc { get; private set; }
  public DateTimeOffset? RevokedUtc { get; private set; }
  public long? ReplacedByRefreshTokenRecordId { get; private set; }
  public byte[] RowVersion { get; private set; } = [];

  internal ReadOnlySpan<byte> SecretHash => secretHash;
  internal RefreshTokenRecord? ReplacedByRefreshTokenRecord => replacedByRefreshTokenRecord;

  internal static RefreshTokenRecord Create(
    long authenticationSessionId,
    Guid publicId,
    byte[] secretHash,
    Guid tokenFamilyId,
    string clientId,
    DateTimeOffset createdUtc,
    DateTimeOffset expiresUtc) => new(
      0,
      authenticationSessionId,
      publicId,
      secretHash,
      tokenFamilyId,
      clientId,
      createdUtc,
      expiresUtc);

  public bool IsActive(DateTimeOffset utcNow) =>
    ConsumedUtc is null && RevokedUtc is null && utcNow.ToUniversalTime() < ExpiresUtc;

  internal Result Consume(DateTimeOffset occurredUtc)
  {
    var utc = occurredUtc.ToUniversalTime();
    if (!IsActive(utc))
    {
      return Result.Failure(AuthenticationErrors.InvalidRefreshToken);
    }

    ConsumedUtc = utc;
    return Result.Success();
  }

  internal void LinkReplacement(RefreshTokenRecord replacement)
  {
    ArgumentNullException.ThrowIfNull(replacement);
    if (replacedByRefreshTokenRecord is not null || replacement.AuthenticationSessionId != AuthenticationSessionId ||
      replacement.TokenFamilyId != TokenFamilyId || !string.Equals(replacement.ClientId, ClientId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("The refresh-token replacement binding is invalid.");
    }

    replacedByRefreshTokenRecord = replacement;
  }

  internal void Revoke(DateTimeOffset occurredUtc)
  {
    if (ConsumedUtc is null && RevokedUtc is null)
    {
      RevokedUtc = occurredUtc.ToUniversalTime();
    }
  }

  private static bool IsValidClientId(string? clientId) =>
    !string.IsNullOrWhiteSpace(clientId) && clientId.Length <= 64 && string.Equals(clientId, clientId.Trim(), StringComparison.Ordinal);
}
