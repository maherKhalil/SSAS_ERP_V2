namespace SSAS.Platform.Domain.Enums;

// An OBSERVED SQL Server recovery model (ADR-022 §9).
//
// Observed and reported, never changed by the platform. Switching a database to FULL starts transaction-log
// growth that will fill a disk unless log backups are already running, so performing that switch
// automatically — on a database that is by definition misconfigured — risks converting a durability gap
// into an outage.
public enum TenantDatabaseRecoveryModel
{
  // Cannot support transaction-log backups. Valid for a full-only or full-plus-differential policy;
  // incompatible with a policy that schedules log backups.
  Simple = 1,

  Full = 2,

  // Supports the log chain, so a log-scheduling policy is NOT invalid on a bulk-logged database
  // (ADR-022 §9, v1.2). The caveat is narrow: intervals containing minimally logged operations restrict
  // exact point-in-time semantics WITHIN those intervals. Phase D verifies the selected managed recovery
  // sequence and claims no arbitrary customer-selected point-in-time recovery through such an interval.
  BulkLogged = 3
}
