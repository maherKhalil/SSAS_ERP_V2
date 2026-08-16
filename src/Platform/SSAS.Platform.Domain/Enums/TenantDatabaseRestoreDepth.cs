namespace SSAS.Platform.Domain.Enums;

// How deeply a restore verification must exercise the chain (ADR-022 §17, TS-Backup Phase D).
//
// Selected by what the ACTIVE POLICY CLAIMS, never by what is cheapest. A policy scheduling log backups
// claims point-in-time recoverability; verifying only its full would prove the baseline restores and leave
// the differential and log path — the part with the most moving pieces — unexercised until an incident.
public enum TenantDatabaseRestoreDepth
{
  // Level A. The policy's required recoverability is full-only.
  Full = 1,

  // Level B. Differential protection is part of the active policy: the full baseline, then the latest
  // APPLICABLE differential taken against that baseline. A differential belonging to another base is not
  // restorable onto this one and is never selected.
  FullWithDifferential = 2,

  // Level C. Transaction-log recovery is part of the active policy: the full baseline, the applicable
  // differential IF ONE APPLIES, then the required subsequent log backups in order, recovering only at the
  // end.
  //
  // A DIFFERENTIAL IS NOT A PRECONDITION. Where none applies, Level C is the full baseline followed by the
  // required logs; preferring the latest applicable differential is a sequence strategy that shortens the
  // log tail, not a requirement.
  FullWithDifferentialAndLog = 3
}
