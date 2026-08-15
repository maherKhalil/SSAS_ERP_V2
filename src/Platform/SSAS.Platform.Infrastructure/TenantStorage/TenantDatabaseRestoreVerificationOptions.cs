namespace SSAS.Platform.Infrastructure.TenantStorage;

// Deployment configuration for restore verification (ADR-022 §17, TS-Backup Phase D).
//
// A SEPARATE SECTION from the backup scheduler, because the two differ in cadence, cost, credential and
// blast radius. Sharing a section would force one set of limits onto both and blur which identity is being
// configured — and one of these two can drop databases.
//
// NOTHING HERE IS A SAFETY CONTROL. There is no switch that disables isolation validation, the reserved
// naming vocabulary or the cleanup conjunction; those are correctness preconditions with no configuration
// path, exactly as the provider's visibility and in-flight checks are (ADR-022 compliance rules 29 and 30).
public sealed class TenantDatabaseRestoreVerificationOptions
{
  public const string SectionName = "TenantStorage:BackupVerification";

  // DEFAULTS OFF. Enabling unattended restore verification also requires a verification server, a
  // privileged verification identity and file roots that do not exist by default, so a host that has not
  // been prepared would otherwise attempt restores it cannot perform.
  public bool Enabled { get; set; }

  // Trusted LOOKUP KEY into VerificationServers — never an address, never a connection string. The single
  // input that selects where a restore happens (ADR-022 §17, compliance rule 33).
  public string? RestoreServerKey { get; set; }

  // Directory the SQL SERVER SERVICE IDENTITY of the VERIFICATION instance can write restored data files
  // to. As with backup destinations, this is the server's access and not the application process's, which
  // is the single most common way a correctly configured restore fails with OS error 5.
  public string? RestoreDataRoot { get; set; }

  public string? RestoreLogRoot { get; set; }

  // NON-PRODUCTION ONLY, and explicit (ADR-022 §17, compliance rule 32).
  //
  // Permits the verification target to be the same instance that hosts authoritative tenant databases. This
  // exists so a developer with one SQL Server can exercise the path; it is NOT a fallback, and nothing
  // reaches it by failing to resolve a dedicated target. Production leaves it false and a missing or
  // unusable target fails closed.
  public bool AllowSameInstanceVerification { get; set; }

  // How long a verification database must have existed before automated cleanup may consider it an orphan.
  // Deliberately generous: a long legitimate restore must never be swept out from under itself.
  public TimeSpan OrphanCleanupGracePeriod { get; set; } = TimeSpan.FromHours(6);
}
