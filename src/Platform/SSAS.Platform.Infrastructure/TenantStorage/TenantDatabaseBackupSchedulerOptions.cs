namespace SSAS.Platform.Infrastructure.TenantStorage;

// Operational tuning for the fleet backup scheduler (ADR-022 §13, TS-Backup Phase C).
//
// EVERYTHING HERE IS A SCHEDULING PREFERENCE, never a safety control. Turning fleet orchestration off, or
// slowing it down, is a legitimate operational decision. It is emphatically NOT the same class of switch as
// the provider's in-flight safety check, which is a correctness precondition and has no disable at all
// (ADR-022 compliance rules 29 and 30).
public sealed class TenantDatabaseBackupSchedulerOptions
{
  // DEFAULTS OFF.
  //
  // Enabling unattended fleet backups is a deployment decision with real consequences — privileged
  // connections, hours-long operations, storage consumption — and it requires configuration that does not
  // exist by default anyway: BackupServers credentials and BackupDestinations. A host that has not been
  // prepared would otherwise start sweeping on first boot and log a failure per database per minute.
  //
  // This mirrors how the estate already treats consequential automation: present, registered, and started
  // deliberately.
  public bool Enabled { get; init; }

  // How often a sweep begins. A fifteen-minute log cadence is the tightest realistic policy, so minute-level
  // resolution is ample and anything faster mostly re-reads a fleet that has not changed.
  public TimeSpan SweepInterval { get; init; } = TimeSpan.FromSeconds(60);

  // Keyset page size. Bounded so a sweep never materialises the estate.
  public int BatchSize { get; init; } = 100;

  // Backups are heavy. This is per application instance, and it is small on purpose.
  public int MaxConcurrentBackups { get; init; } = 2;

  // PER SQL SERVER, and the cap that matters most. Shared hosting concentrates many tenant databases behind
  // one ServerKey, where a global cap alone would happily run several fulls against the same spindles. One
  // at a time per server is the conservative default; raise it only with evidence from a real estate.
  public int MaxConcurrentPerServer { get; init; } = 1;

  // Delay before the first sweep, so a starting host is not competing with its own warm-up.
  public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);

  // Bounded randomisation applied to the startup delay and to each interval. Several instances deployed
  // together would otherwise sweep in lockstep forever, converting independent workers into a thundering
  // herd against one SQL Server.
  public TimeSpan MaximumJitter { get; init; } = TimeSpan.FromSeconds(30);

  // First pause after a failed backup, doubling per consecutive failure up to the cap.
  public TimeSpan FailureInitialBackoff { get; init; } = TimeSpan.FromMinutes(5);

  public TimeSpan FailureMaximumBackoff { get; init; } = TimeSpan.FromMinutes(60);

  // How far back to look when counting consecutive failures for the backoff curve. Bounded so the lookup
  // stays a small indexed read.
  public int FailureHistoryDepth { get; init; } = 5;

  public static TenantDatabaseBackupSchedulerOptions Default { get; } = new();
}
