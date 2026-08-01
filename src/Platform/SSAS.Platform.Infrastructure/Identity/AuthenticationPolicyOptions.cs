namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AuthenticationPolicyOptions
{
  public const string SectionName = "Authentication:Policy";

  public int MinimumPasswordLength { get; set; } = 12;

  public int MaximumPasswordLength { get; set; } = 128;

  public int FailedAttemptThreshold { get; set; } = 5;

  public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

  public int FailedAttemptConcurrencyRetries { get; set; } = 3;

  public TimeSpan InvitationLifetime { get; set; } = TimeSpan.FromHours(24);

  public TimeSpan PasswordResetLifetime { get; set; } = TimeSpan.FromMinutes(30);
}
