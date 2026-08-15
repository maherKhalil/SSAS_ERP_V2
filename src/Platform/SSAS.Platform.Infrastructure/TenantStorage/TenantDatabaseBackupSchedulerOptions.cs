namespace SSAS.Platform.Infrastructure.TenantStorage;

// Deployment configuration for the fleet backup scheduler (ADR-022 §13, TS-Backup Phase C).
//
// EVERYTHING HERE IS A SCHEDULING PREFERENCE, never a safety control. Turning fleet orchestration off, or
// slowing it down, is a legitimate operational decision. It is emphatically NOT the same class of switch as
// the provider's visibility and in-flight checks, which are correctness preconditions with no configuration
// path at all (ADR-022 compliance rules 29 and 30).
//
// Bound from configuration and validated at startup, because an operator who cannot enable the scheduler
// without a rebuild does not have a scheduler.
public sealed class TenantDatabaseBackupSchedulerOptions
{
  public const string SectionName = "TenantStorage:BackupScheduler";

  // DEFAULTS OFF, and must be enabled deliberately.
  //
  // Unattended fleet backups need credentials and destinations that do not exist by default, so a host that
  // has not been prepared would otherwise sweep on first boot and fail once per database per minute. The
  // scheduler says so clearly at startup rather than being silently absent.
  public bool Enabled { get; set; }

  // How often a sweep begins. A fifteen-minute log cadence is the tightest realistic policy, so minute-level
  // resolution is ample.
  public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(60);

  // Keyset page size, per server. Bounded so a sweep never materialises the estate.
  public int BatchSize { get; set; } = 100;

  // Backups are heavy. Per application instance, and small on purpose.
  public int MaxConcurrentBackups { get; set; } = 2;

  // PER SQL SERVER, and the cap that matters most. Shared hosting concentrates many tenant databases behind
  // one ServerKey, where a global cap alone would happily run several fulls against the same spindles.
  public int MaxConcurrentPerServer { get; set; } = 1;

  // Delay before the first sweep, so a starting host is not competing with its own warm-up.
  public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);

  // Bounded randomisation on the startup delay and each interval. Instances deployed together would
  // otherwise sweep in lockstep forever.
  public TimeSpan MaximumJitter { get; set; } = TimeSpan.FromSeconds(30);

  // How long a database is left alone after a FAILED backup.
  //
  // Deliberately flat rather than escalating. The v1 shape carried a consecutive-failure curve and a history
  // depth, neither of which production ever computed — the escalation was live only in its own unit tests.
  // A single honest interval is easier to reason about and does not require counting failure streaks across
  // the fleet on every sweep.
  public TimeSpan FailureRetryBackoff { get; set; } = TimeSpan.FromMinutes(5);

  // How long after a controlled skip — ownership held elsewhere, an operation already in flight, or a
  // decision another instance already satisfied — before this database is considered again. Short, because
  // a skip means coordination worked.
  public TimeSpan SkipRetryBackoff { get; set; } = TimeSpan.FromMinutes(1);
}
