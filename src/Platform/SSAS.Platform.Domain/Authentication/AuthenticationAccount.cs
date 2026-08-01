using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Authentication;

public sealed class AuthenticationAccount : AggregateRoot<long>, IAuditableEntity
{
  private string normalizedLoginEmail = string.Empty;
  private string? passwordHash;

  private AuthenticationAccount(long id, long identityId, LoginEmail loginEmail)
    : base(id)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identityId);

    IdentityId = identityId;
    LoginEmail = loginEmail;
    normalizedLoginEmail = loginEmail.NormalizedValue;
    Status = AuthenticationAccountStatus.PendingSetup;
    SecurityVersion = 1;
  }

  private AuthenticationAccount()
    : base(0)
  {
    LoginEmail = null!;
  }

  public long IdentityId { get; private set; }

  public LoginEmail LoginEmail { get; private set; }

  public string NormalizedLoginEmail => normalizedLoginEmail;

  public DateTimeOffset? EmailVerifiedUtc { get; private set; }

  public AuthenticationAccountStatus Status { get; private set; }

  public int FailedAttemptCount { get; private set; }

  public DateTimeOffset? LockoutEndUtc { get; private set; }

  public long SecurityVersion { get; private set; }

  public DateTimeOffset? PasswordChangedUtc { get; private set; }

  public bool HasPassword => passwordHash is not null;

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  internal string? PasswordHash => passwordHash;

  public static AuthenticationAccount CreatePending(long identityId, LoginEmail loginEmail) =>
    new(0, identityId, loginEmail);

  public bool IsLocked(DateTimeOffset utcNow) => LockoutEndUtc is { } lockoutEnd && lockoutEnd > utcNow.ToUniversalTime();

  public bool CanVerifyPassword(DateTimeOffset utcNow) =>
    Status == AuthenticationAccountStatus.Active &&
    EmailVerifiedUtc.HasValue &&
    passwordHash is not null &&
    !IsLocked(utcNow);

  public bool CanIssuePasswordReset =>
    Status == AuthenticationAccountStatus.Active && EmailVerifiedUtc.HasValue && passwordHash is not null;

  public bool IsAuthenticationEligible =>
    Status == AuthenticationAccountStatus.Active && EmailVerifiedUtc.HasValue && passwordHash is not null;

  public Result CompleteInitialSetup(
    string newPasswordHash,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != AuthenticationAccountStatus.PendingSetup || passwordHash is not null || string.IsNullOrWhiteSpace(newPasswordHash))
    {
      return Result.Failure(AuthenticationErrors.InvalidAccountTransition);
    }

    var utc = occurredUtc.ToUniversalTime();
    passwordHash = newPasswordHash;
    EmailVerifiedUtc = utc;
    PasswordChangedUtc = utc;
    Status = AuthenticationAccountStatus.Active;
    FailedAttemptCount = 0;
    LockoutEndUtc = null;
    RaiseDomainEvent(new AuthenticationAccountSetupCompleted(eventId, utc, IdentityId, SecurityVersion));
    return Result.Success();
  }

  public Result RecordFailedAttempt(
    int lockoutThreshold,
    TimeSpan lockoutDuration,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != AuthenticationAccountStatus.Active || lockoutThreshold < 1 || lockoutDuration <= TimeSpan.Zero)
    {
      return Result.Failure(AuthenticationErrors.InvalidAccountTransition);
    }

    var utc = occurredUtc.ToUniversalTime();
    if (LockoutEndUtc is { } expiredLockout && expiredLockout <= utc)
    {
      FailedAttemptCount = 0;
      LockoutEndUtc = null;
    }

    FailedAttemptCount++;
    if (FailedAttemptCount >= lockoutThreshold)
    {
      FailedAttemptCount = lockoutThreshold;
      LockoutEndUtc = utc.Add(lockoutDuration);
      RaiseDomainEvent(new AuthenticationAccountLocked(eventId, utc, Id, LockoutEndUtc.Value));
    }
    else
    {
      RaiseDomainEvent(new AuthenticationCredentialFailureRecorded(eventId, utc, Id, FailedAttemptCount));
    }

    return Result.Success();
  }

  public void RecordSuccessfulVerification()
  {
    FailedAttemptCount = 0;
    LockoutEndUtc = null;
  }

  public Result ReplaceHashAfterSuccessfulVerification(
    string newPasswordHash,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != AuthenticationAccountStatus.Active || string.IsNullOrWhiteSpace(newPasswordHash))
    {
      return Result.Failure(AuthenticationErrors.InvalidAccountTransition);
    }

    passwordHash = newPasswordHash;
    RaiseDomainEvent(new AuthenticationPasswordRehashed(eventId, occurredUtc, Id, SecurityVersion));
    return Result.Success();
  }

  public Result ResetPassword(
    string newPasswordHash,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != AuthenticationAccountStatus.Active || string.IsNullOrWhiteSpace(newPasswordHash))
    {
      return Result.Failure(AuthenticationErrors.InvalidAccountTransition);
    }

    var utc = occurredUtc.ToUniversalTime();
    passwordHash = newPasswordHash;
    PasswordChangedUtc = utc;
    FailedAttemptCount = 0;
    LockoutEndUtc = null;
    SecurityVersion++;
    RaiseDomainEvent(new AuthenticationPasswordReset(eventId, utc, Id, SecurityVersion));
    return Result.Success();
  }

  public Result Disable(Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != AuthenticationAccountStatus.Active)
    {
      return Result.Failure(AuthenticationErrors.InvalidAccountTransition);
    }

    Status = AuthenticationAccountStatus.Disabled;
    SecurityVersion++;
    RaiseDomainEvent(new AuthenticationAccountDisabled(eventId, occurredUtc, Id, SecurityVersion));
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
}
