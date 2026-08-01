namespace SSAS.Platform.Application.Authentication;

public sealed class AuthenticationPolicy
{
  public const int DefaultMinimumPasswordLength = 12;
  public const int DefaultMaximumPasswordLength = 128;
  public const int DefaultFailedAttemptThreshold = 5;
  public const int DefaultFailedAttemptConcurrencyRetries = 3;
  public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);
  public static readonly TimeSpan DefaultInvitationLifetime = TimeSpan.FromHours(24);
  public static readonly TimeSpan DefaultPasswordResetLifetime = TimeSpan.FromMinutes(30);

  public AuthenticationPolicy(
    int minimumPasswordLength = DefaultMinimumPasswordLength,
    int maximumPasswordLength = DefaultMaximumPasswordLength,
    int failedAttemptThreshold = DefaultFailedAttemptThreshold,
    TimeSpan? lockoutDuration = null,
    int failedAttemptConcurrencyRetries = DefaultFailedAttemptConcurrencyRetries,
    TimeSpan? invitationLifetime = null,
    TimeSpan? passwordResetLifetime = null)
  {
    LockoutDuration = lockoutDuration ?? DefaultLockoutDuration;
    InvitationLifetime = invitationLifetime ?? DefaultInvitationLifetime;
    PasswordResetLifetime = passwordResetLifetime ?? DefaultPasswordResetLifetime;
    if (minimumPasswordLength < 12 || maximumPasswordLength < 64 || maximumPasswordLength < minimumPasswordLength ||
      failedAttemptThreshold < 1 || LockoutDuration <= TimeSpan.Zero || failedAttemptConcurrencyRetries is < 0 or > 3 ||
      InvitationLifetime <= TimeSpan.Zero || PasswordResetLifetime <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(nameof(minimumPasswordLength), "Authentication policy values are outside approved bounds.");
    }

    MinimumPasswordLength = minimumPasswordLength;
    MaximumPasswordLength = maximumPasswordLength;
    FailedAttemptThreshold = failedAttemptThreshold;
    FailedAttemptConcurrencyRetries = failedAttemptConcurrencyRetries;
  }

  public int MinimumPasswordLength { get; }

  public int MaximumPasswordLength { get; }

  public int FailedAttemptThreshold { get; }

  public TimeSpan LockoutDuration { get; }

  public int FailedAttemptConcurrencyRetries { get; }

  public TimeSpan InvitationLifetime { get; }

  public TimeSpan PasswordResetLifetime { get; }
}
