namespace SSAS.Platform.Domain.Enums;

// Disposal state of the disposable verification database (ADR-022 §17, TS-Backup Phase D).
//
// A SEPARATE DIMENSION from the verification result, and that separation is the point. Where a restore and
// its probes succeed but the drop fails, the recovery evidence remains valid — the chain was restored and
// the database was proven usable, and that does not become untrue because cleanup failed afterwards.
// Collapsing the two would force a choice between concealing an orphan and discarding recovery proof, and
// ADR-022 accepts neither.
public enum TenantDatabaseVerificationCleanupState
{
  // No verification database has been created yet, so there is nothing to dispose of.
  NotRequired = 1,

  // A verification database exists and has not yet been removed. THIS is the state an orphan is found in
  // after a crash, and the reason the name is persisted before the database is created.
  Pending = 2,

  Succeeded = 3,

  // The database could not be removed. Recovery evidence is unaffected; this is an operational condition
  // that surfaces an orphan for reconciliation.
  Failed = 4
}
