using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Domain.PlatformSupport;

// Platform-plane refresh-token record (ADR-016 Phase 3C / DEC-TEN-0022). A separate child of
// PlatformAuthenticationSession — never a polymorphic reuse of the tenant RefreshTokenRecord — so a
// platform refresh token can only ever resolve against platform-session persistence (structural cross-plane
// isolation). Mirrors the mature one-time rotation / reuse-detection semantics of the tenant record.
public sealed class PlatformRefreshTokenRecord : Entity<long>
{
  private byte[] secretHash = [];
  private PlatformRefreshTokenRecord? replacedByRefreshTokenRecord;

  private PlatformRefreshTokenRecord(
    long id,
    long platformAuthenticationSessionId,
    Guid publicId,
    byte[] secretHash,
    Guid tokenFamilyId,
    string clientId,
    DateTimeOffset createdUtc,
    DateTimeOffset expiresUtc)
    : base(id)
  {
    if (platformAuthenticationSessionId <= 0 || publicId == Guid.Empty || secretHash.Length != 32 ||
      tokenFamilyId == Guid.Empty || !IsValidClientId(clientId) || expiresUtc <= createdUtc)
    {
      throw new ArgumentException("The platform refresh-token record values are invalid.");
    }

    PlatformAuthenticationSessionId = platformAuthenticationSessionId;
    PublicId = publicId;
    this.secretHash = secretHash.ToArray();
    TokenFamilyId = tokenFamilyId;
    ClientId = clientId;
    CreatedUtc = createdUtc.ToUniversalTime();
    ExpiresUtc = expiresUtc.ToUniversalTime();
  }

  private PlatformRefreshTokenRecord()
    : base(0)
  {
    ClientId = string.Empty;
  }

  public long PlatformAuthenticationSessionId { get; private set; }
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
  internal PlatformRefreshTokenRecord? ReplacedByRefreshTokenRecord => replacedByRefreshTokenRecord;

  internal static PlatformRefreshTokenRecord Create(
    long platformAuthenticationSessionId,
    Guid publicId,
    byte[] secretHash,
    Guid tokenFamilyId,
    string clientId,
    DateTimeOffset createdUtc,
    DateTimeOffset expiresUtc) => new(
      0,
      platformAuthenticationSessionId,
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

  internal void LinkReplacement(PlatformRefreshTokenRecord replacement)
  {
    ArgumentNullException.ThrowIfNull(replacement);
    if (replacedByRefreshTokenRecord is not null ||
      replacement.PlatformAuthenticationSessionId != PlatformAuthenticationSessionId ||
      replacement.TokenFamilyId != TokenFamilyId ||
      !string.Equals(replacement.ClientId, ClientId, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("The platform refresh-token replacement binding is invalid.");
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
