namespace SSAS.Platform.Domain.Enums;

// The FOURTH operational dimension (ADR-022 §2, §6) — durability, independent of the three ADR-018
// dimensions. Never merged into SchemaCompatibilityStatus, never merged into ConnectivityStatus, and never
// collapsed into a general IsHealthy/IsReady flag.
//
// The combinations this separation exists to express are ordinary rather than exotic: schema-compatible but
// not recovery-ready (a freshly migrated dedicated database with no baseline), recovery-ready but
// schema-incompatible (a well-protected database awaiting a release), and backup-degraded while serving
// normally (a late log backup on a healthy database).
//
// Each value maps to a DIFFERENT operator action, which is what makes them worth discriminating rather than
// folding into a reason code.
public enum TenantDatabaseRecoveryReadinessStatus
{
  // Never evaluated, or evaluation could not complete. The pre-verification state, and the value every
  // existing database backfills to: nothing has proven those databases recoverable.
  Unknown = 1,

  // Current EVIDENCE satisfies the configured policy. Never "the last backup command returned success",
  // and never inferred from a policy existing.
  Protected = 2,

  // Protected but slipping — a backup type is overdue, or verification is aging toward its limit.
  Degraded = 3,

  // No usable recovery position: no baseline, or a known-broken chain.
  Unprotected = 4,

  // The database's recovery model cannot support what the policy requires. Detected and reported; the
  // platform never changes a recovery model automatically (ADR-022 §9).
  RecoveryModelInvalid = 5,

  // Backups exist but have not been verified within the policy's interval.
  VerificationOverdue = 6
}
