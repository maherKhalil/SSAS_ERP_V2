namespace SSAS.Platform.Domain.Enums;

// EXECUTION status of one provider backup operation (ADR-022 §15).
//
// Deliberately NOT recovery readiness. These describe what happened to a single run; readiness is derived
// from accumulated evidence across many. Merging them would let one successful command claim protection.
public enum TenantDatabaseBackupRunStatus
{
  // Recorded, not yet started.
  Pending = 1,

  Running = 2,

  // Established from reconciled post-operation provider evidence (ADR-022 §14, §15) — never from a command
  // having been submitted or an ExecuteNonQuery having returned.
  Succeeded = 3,

  Failed = 4,

  // Another platform worker holds backup ownership of this physical database. NOT a failure: coordination
  // worked exactly as intended.
  SkippedOwnershipHeld = 5,

  // A server-side backup was already in flight against this database (ADR-022 §14). NOT a failure either —
  // recording it as one would degrade recovery readiness on the strength of successful coordination.
  // Final vocabulary for this case is settled in Phase B alongside the provider.
  SkippedInFlightOperation = 6,

  // Policy forbids this operation right now — disabled, or an authority that does not permit execution.
  BlockedByPolicy = 7,

  // The operation completed but its verification did not.
  VerificationFailed = 8
}
