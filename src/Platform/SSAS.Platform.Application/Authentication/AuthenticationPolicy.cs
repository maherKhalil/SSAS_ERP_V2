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
  public static readonly TimeSpan DefaultSessionIdleLifetime = TimeSpan.FromDays(30);
  public static readonly TimeSpan DefaultSessionAbsoluteLifetime = TimeSpan.FromDays(90);
  public static readonly TimeSpan DefaultTenantSelectionLifetime = TimeSpan.FromMinutes(5);
  public const int DefaultMaximumActiveSessions = 10;

  public AuthenticationPolicy(
    int minimumPasswordLength = DefaultMinimumPasswordLength,
    int maximumPasswordLength = DefaultMaximumPasswordLength,
    int failedAttemptThreshold = DefaultFailedAttemptThreshold,
    TimeSpan? lockoutDuration = null,
    int failedAttemptConcurrencyRetries = DefaultFailedAttemptConcurrencyRetries,
    TimeSpan? invitationLifetime = null,
    TimeSpan? passwordResetLifetime = null,
    TimeSpan? sessionIdleLifetime = null,
    TimeSpan? sessionAbsoluteLifetime = null,
    TimeSpan? tenantSelectionLifetime = null,
    int maximumActiveSessions = DefaultMaximumActiveSessions)
  {
    LockoutDuration = lockoutDuration ?? DefaultLockoutDuration;
    InvitationLifetime = invitationLifetime ?? DefaultInvitationLifetime;
    PasswordResetLifetime = passwordResetLifetime ?? DefaultPasswordResetLifetime;
    SessionIdleLifetime = sessionIdleLifetime ?? DefaultSessionIdleLifetime;
    SessionAbsoluteLifetime = sessionAbsoluteLifetime ?? DefaultSessionAbsoluteLifetime;
    TenantSelectionLifetime = tenantSelectionLifetime ?? DefaultTenantSelectionLifetime;
    if (minimumPasswordLength < 12 || maximumPasswordLength < 64 || maximumPasswordLength < minimumPasswordLength ||
      failedAttemptThreshold < 1 || LockoutDuration <= TimeSpan.Zero || failedAttemptConcurrencyRetries is < 0 or > 3 ||
      InvitationLifetime <= TimeSpan.Zero || PasswordResetLifetime <= TimeSpan.Zero ||
      SessionIdleLifetime <= TimeSpan.Zero || SessionAbsoluteLifetime <= TimeSpan.Zero ||
      SessionIdleLifetime > SessionAbsoluteLifetime || TenantSelectionLifetime <= TimeSpan.Zero ||
      maximumActiveSessions is < 1 or > DefaultMaximumActiveSessions)
    {
      throw new ArgumentOutOfRangeException(nameof(minimumPasswordLength), "Authentication policy values are outside approved bounds.");
    }

    MinimumPasswordLength = minimumPasswordLength;
    MaximumPasswordLength = maximumPasswordLength;
    FailedAttemptThreshold = failedAttemptThreshold;
    FailedAttemptConcurrencyRetries = failedAttemptConcurrencyRetries;
    MaximumActiveSessions = maximumActiveSessions;
  }

  public int MinimumPasswordLength { get; }

  public int MaximumPasswordLength { get; }

  public int FailedAttemptThreshold { get; }

  public TimeSpan LockoutDuration { get; }

  public int FailedAttemptConcurrencyRetries { get; }

  public TimeSpan InvitationLifetime { get; }

  public TimeSpan PasswordResetLifetime { get; }

  public TimeSpan SessionIdleLifetime { get; }

  public TimeSpan SessionAbsoluteLifetime { get; }

  public TimeSpan TenantSelectionLifetime { get; }

  public int MaximumActiveSessions { get; }
}
