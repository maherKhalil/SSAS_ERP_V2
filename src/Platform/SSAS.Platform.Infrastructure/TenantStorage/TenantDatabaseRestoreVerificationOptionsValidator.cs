using Microsoft.Extensions.Options;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Startup validation for restore-verification configuration (ADR-022 §17, TS-Backup Phase D).
//
// Validated at startup rather than defended at use, following the scheduler precedent: a deployment that
// enables unattended restore verification without a target or file roots should fail to start and say why,
// not discover it on the first sweep. NO SILENT CLAMPING — an operator's mistake must not become behaviour
// nobody asked for.
//
// Validation applies ONLY when enabled. A disabled deployment with empty values is an ordinary host, and
// refusing to start it would punish everyone not using restore verification.
public sealed class TenantDatabaseRestoreVerificationOptionsValidator
  : IValidateOptions<TenantDatabaseRestoreVerificationOptions>
{
  public ValidateOptionsResult Validate(string? name, TenantDatabaseRestoreVerificationOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (!options.Enabled)
    {
      return ValidateOptionsResult.Success;
    }

    var section = TenantDatabaseRestoreVerificationOptions.SectionName;
    var failures = new List<string>();

    // FAILS CLOSED at startup rather than at the first restore (ADR-022 compliance rule 44). A deployment
    // that enables verification without naming a target has not configured verification, and there is no
    // fallback to the source database's server for it to reach instead.
    if (string.IsNullOrWhiteSpace(options.RestoreServerKey))
    {
      failures.Add(
        $"{section}:{nameof(options.RestoreServerKey)} is required when restore verification is enabled; " +
        "there is no fallback to the tenant database's own server.");
    }

    if (string.IsNullOrWhiteSpace(options.RestoreDataRoot))
    {
      failures.Add($"{section}:{nameof(options.RestoreDataRoot)} is required when restore verification is enabled.");
    }

    if (string.IsNullOrWhiteSpace(options.RestoreLogRoot))
    {
      failures.Add($"{section}:{nameof(options.RestoreLogRoot)} is required when restore verification is enabled.");
    }

    if (options.OrphanCleanupGracePeriod <= TimeSpan.Zero)
    {
      failures.Add(
        $"{section}:{nameof(options.OrphanCleanupGracePeriod)} must be greater than zero; a non-positive " +
        "grace period would make a verification database eligible for deletion while it is still in use.");
    }

    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }
}
