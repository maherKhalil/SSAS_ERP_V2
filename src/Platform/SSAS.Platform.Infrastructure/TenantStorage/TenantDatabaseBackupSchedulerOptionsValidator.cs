using Microsoft.Extensions.Options;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Startup validation for fleet scheduler configuration (ADR-022 §13, TS-Backup Phase C).
//
// Validated at startup rather than defended at use. Once these values come from configuration, clamping them
// silently at the point of use — `Math.Max(1, …)` and friends — turns an operator's mistake into behaviour
// nobody asked for: a zero concurrency cap quietly becoming one, or a negative interval quietly becoming a
// hot loop. A deployment that misconfigures unattended backups should fail to start and say why.
//
// Validation applies ONLY when the scheduler is enabled. A disabled scheduler with default values is a
// perfectly ordinary host, and refusing to start it would punish every deployment that does not use fleet
// backups at all.
public sealed class TenantDatabaseBackupSchedulerOptionsValidator
  : IValidateOptions<TenantDatabaseBackupSchedulerOptions>
{
  public ValidateOptionsResult Validate(string? name, TenantDatabaseBackupSchedulerOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (!options.Enabled)
    {
      return ValidateOptionsResult.Success;
    }

    var section = TenantDatabaseBackupSchedulerOptions.SectionName;
    var failures = new List<string>();

    if (options.SweepInterval <= TimeSpan.Zero)
    {
      failures.Add($"{section}:{nameof(options.SweepInterval)} must be greater than zero.");
    }

    if (options.BatchSize <= 0)
    {
      failures.Add($"{section}:{nameof(options.BatchSize)} must be greater than zero.");
    }

    // Zero concurrency would not merely be slow — a semaphore with no permits never admits anyone, so the
    // sweep would hang rather than do nothing visible.
    if (options.MaxConcurrentBackups <= 0)
    {
      failures.Add($"{section}:{nameof(options.MaxConcurrentBackups)} must be greater than zero.");
    }

    if (options.MaxConcurrentPerServer <= 0)
    {
      failures.Add($"{section}:{nameof(options.MaxConcurrentPerServer)} must be greater than zero.");
    }

    if (options.MaxConcurrentPerServer > options.MaxConcurrentBackups)
    {
      failures.Add(
        $"{section}:{nameof(options.MaxConcurrentPerServer)} must not exceed " +
        $"{nameof(options.MaxConcurrentBackups)}; a per-server cap above the global cap can never be reached.");
    }

    if (options.StartupDelay < TimeSpan.Zero)
    {
      failures.Add($"{section}:{nameof(options.StartupDelay)} must not be negative.");
    }

    if (options.MaximumJitter < TimeSpan.Zero)
    {
      failures.Add($"{section}:{nameof(options.MaximumJitter)} must not be negative.");
    }

    if (options.FailureRetryBackoff <= TimeSpan.Zero)
    {
      failures.Add($"{section}:{nameof(options.FailureRetryBackoff)} must be greater than zero.");
    }

    if (options.SkipRetryBackoff <= TimeSpan.Zero)
    {
      failures.Add($"{section}:{nameof(options.SkipRetryBackoff)} must be greater than zero.");
    }

    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }
}
