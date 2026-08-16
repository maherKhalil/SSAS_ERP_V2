namespace SSAS.Platform.Domain.TenantStorage;

// How far a restore verification got before failing (ADR-022 §17, v1.2).
//
// These are NOT interchangeable, and the reason they are an enum rather than a boolean is that each maps to
// a different readiness verdict. A verification that failed at the baseline and one that failed only its
// cleanup are separated by the whole question the capability exists to answer.
public enum TenantDatabaseVerificationFailure
{
  // The required full baseline itself could not be restored.
  BaselineRestoreFailed = 1,

  // The baseline restored, but a required differential or log segment could not be applied or validated.
  DeeperChainRestoreFailed = 2,

  // The platform-managed artifacts cannot reconstruct the required recovery path at all. Distinct from the
  // above because it is a statement about the CHAIN rather than about one restore attempt.
  RequiredChainBreak = 3,

  // The restore completed and the database came online, but a required migration-history, schema or
  // usability probe failed. Restoring to something unusable is not a recovery position.
  PostRestoreProbeFailed = 4,

  // Restore and validation succeeded; only disposal failed. Readiness is unaffected.
  CleanupFailed = 5,

  // The attempt could not begin or complete for reasons independent of the artifacts. NOT evidence about
  // the backup in either direction.
  VerificationInfrastructureUnavailable = 6
}
