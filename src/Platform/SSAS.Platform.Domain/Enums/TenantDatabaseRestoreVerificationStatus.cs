namespace SSAS.Platform.Domain.Enums;

// EXECUTION status of one actual restore-verification operation (ADR-022 §17, TS-Backup Phase D).
//
// Deliberately SEPARATE from TenantDatabaseBackupRunStatus. A backup and a restore verification are
// different operations with different lifecycles, different servers and different failure meanings, and the
// one thing this status must express that a backup status never has to is that an operation is CURRENTLY
// HOLDING a disposable database — which is what makes crash recovery and destructive cleanup decidable.
//
// Kept small on purpose. Cleanup is NOT modelled here: it is its own dimension
// (TenantDatabaseVerificationCleanupState), because a failed cleanup must not erase a proven restore.
public enum TenantDatabaseRestoreVerificationStatus
{
  // Admitted as THE effective operation for this database's due verification state, and not yet started.
  // Admission is the serialising event (ADR-022 §17, compliance rule 43): reaching this state at all means
  // no other instance holds the same due state.
  Admitted = 1,

  // A restore is in progress. A row left here by a crashed process is exactly the case the orphan sweep
  // exists to reconcile, which is why the record is written before the restore begins rather than after.
  Restoring = 2,

  // The selected chain restored, the database came online, and the required probes succeeded. The only
  // status that constitutes recovery evidence.
  Succeeded = 3,

  // The verification ran and did not establish recoverability. What it means for readiness depends on how
  // deep it reached — see TenantDatabaseRecoveryReadinessEvaluator and ADR-022 §17.
  Failed = 4,

  // The attempt could not begin or complete for reasons independent of the artifacts — verification host
  // unavailable, configuration unresolvable, artifact temporarily unreachable.
  //
  // A SEPARATE STATUS because it is NOT evidence about the backup (ADR-022 §17). Folding it into `Failed`
  // would let a verification-host outage degrade a well-protected database to `Unprotected`, which is
  // reporting a durability emergency on the strength of an unrelated failure.
  InfrastructureUnavailable = 5
}
