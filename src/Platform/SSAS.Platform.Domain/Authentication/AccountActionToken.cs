using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;

namespace SSAS.Platform.Domain.Authentication;

public sealed class AccountActionToken : AggregateRoot<long>, IAuditableEntity
{
  private byte[] secretHash = [];

  private AccountActionToken(
    long id,
    Guid publicId,
    byte[] secretHash,
    AccountActionTokenPurpose purpose,
    long identityId,
    long authenticationAccountId,
    Guid? tenantId,
    long? tenantUserId,
    DateTimeOffset issuedUtc,
    DateTimeOffset expiresUtc)
    : base(id)
  {
    ValidateCreation(publicId, secretHash, purpose, identityId, authenticationAccountId, tenantId, tenantUserId, issuedUtc, expiresUtc);
    PublicId = publicId;
    this.secretHash = secretHash.ToArray();
    Purpose = purpose;
    IdentityId = identityId;
    AuthenticationAccountId = authenticationAccountId;
    TenantId = tenantId;
    TenantUserId = tenantUserId;
    IssuedUtc = issuedUtc.ToUniversalTime();
    ExpiresUtc = expiresUtc.ToUniversalTime();
  }

  private AccountActionToken()
    : base(0)
  {
  }

  public Guid PublicId { get; private set; }

  public AccountActionTokenPurpose Purpose { get; private set; }

  public long IdentityId { get; private set; }

  public long AuthenticationAccountId { get; private set; }

  public Guid? TenantId { get; private set; }

  public long? TenantUserId { get; private set; }

  public DateTimeOffset IssuedUtc { get; private set; }

  public DateTimeOffset ExpiresUtc { get; private set; }

  public DateTimeOffset? ConsumedUtc { get; private set; }

  public DateTimeOffset? RevokedUtc { get; private set; }

  public string? RevokedBy { get; private set; }

  public string? RevocationReason { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  internal ReadOnlySpan<byte> SecretHash => secretHash;

  public static AccountActionToken CreateInvitation(
    Guid publicId,
    byte[] secretHash,
    long identityId,
    long authenticationAccountId,
    Guid tenantId,
    long tenantUserId,
    DateTimeOffset issuedUtc,
    DateTimeOffset expiresUtc,
    Guid eventId) => Create(
      new AccountActionToken(
        0,
        publicId,
        secretHash,
        AccountActionTokenPurpose.Invitation,
        identityId,
        authenticationAccountId,
        tenantId,
        tenantUserId,
        issuedUtc,
        expiresUtc),
      eventId);

  public static AccountActionToken CreatePasswordReset(
    Guid publicId,
    byte[] secretHash,
    long identityId,
    long authenticationAccountId,
    DateTimeOffset issuedUtc,
    DateTimeOffset expiresUtc,
    Guid eventId) => Create(
      new AccountActionToken(
        0,
        publicId,
        secretHash,
        AccountActionTokenPurpose.PasswordReset,
        identityId,
        authenticationAccountId,
        null,
        null,
        issuedUtc,
        expiresUtc),
      eventId);

  public bool IsActive(DateTimeOffset utcNow) =>
    ConsumedUtc is null && RevokedUtc is null && ExpiresUtc > utcNow.ToUniversalTime();

  public Result ValidateForUse(AccountActionTokenPurpose expectedPurpose, DateTimeOffset utcNow)
  {
    return Purpose == expectedPurpose && IsActive(utcNow)
      ? Result.Success()
      : Result.Failure(AuthenticationErrors.InvalidActionToken);
  }

  public Result Consume(Guid eventId, DateTimeOffset occurredUtc)
  {
    var utc = occurredUtc.ToUniversalTime();
    if (!IsActive(utc))
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    ConsumedUtc = utc;
    RaiseDomainEvent(new AccountActionTokenConsumed(eventId, utc, PublicId, Purpose, IdentityId));
    return Result.Success();
  }

  public Result Revoke(string? actor, string reason, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (ConsumedUtc.HasValue || RevokedUtc.HasValue || string.IsNullOrWhiteSpace(reason))
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var utc = occurredUtc.ToUniversalTime();
    RevokedUtc = utc;
    RevokedBy = string.IsNullOrWhiteSpace(actor) ? null : actor;
    RevocationReason = reason.Trim();
    RaiseDomainEvent(new AccountActionTokenRevoked(eventId, utc, PublicId, Purpose, IdentityId));
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

  private static void ValidateCreation(
    Guid publicId,
    byte[] hash,
    AccountActionTokenPurpose purpose,
    long identityId,
    long authenticationAccountId,
    Guid? tenantId,
    long? tenantUserId,
    DateTimeOffset issuedUtc,
    DateTimeOffset expiresUtc)
  {
    if (publicId == Guid.Empty || hash.Length != 32 || identityId <= 0 || authenticationAccountId <= 0 || expiresUtc <= issuedUtc)
    {
      throw new ArgumentException("The action token values are invalid.");
    }

    // ⚠ THIS BINDING HOLDS UP A UNIQUE INDEX (items 180, 181). `AccountActionTokenConfiguration`'s index
    // on `(Purpose, TenantId, TenantUserId)` is filtered on `[TenantUserId] IS NOT NULL` and never
    // mentions `TenantId`, which is nullable. That index is correct ONLY because these two are set
    // together or not at all -- so admitting a mismatch makes a unique index over a nullable column
    // unfiltered, and the second such row is refused at insert.
    //
    // The check below is a backstop: the public factories make a mismatch unconstructible, and
    // `AccountActionTokenBindingArchitectureTests` guards those signatures.
    var invitationBindingIsValid = purpose == AccountActionTokenPurpose.Invitation &&
      tenantId is { } invitationTenantId && invitationTenantId != Guid.Empty && tenantUserId is > 0;
    var resetBindingIsValid = purpose == AccountActionTokenPurpose.PasswordReset && tenantId is null && tenantUserId is null;
    if (!invitationBindingIsValid && !resetBindingIsValid)
    {
      throw new ArgumentException("The action token ownership binding is invalid.");
    }
  }

  private static AccountActionToken Create(AccountActionToken token, Guid eventId)
  {
    token.RaiseDomainEvent(new AccountActionTokenIssued(
      eventId,
      token.IssuedUtc,
      token.PublicId,
      token.Purpose,
      token.IdentityId));
    return token;
  }
}
