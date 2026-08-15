using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// Decides whether a DUE database should nevertheless be left alone for now (ADR-022 §13, TS-Backup Phase C).
//
// PROTECTION DUE AND RETRY BACKOFF ARE SEPARATE CONCEPTS, and keeping them separate is the whole point of
// this type. Due-ness is derived from the last SUCCESSFUL backup; backoff is derived from the last ATTEMPT.
// Collapsing them breaks in one of two ways:
//
//   - deriving due-ness from the last attempt hides a long unprotected gap behind a busy failure loop;
//   - deriving backoff from the last success retries a failing database on every single sweep.
//
// So a database stays overdue while it is failing — the readiness signal keeps telling the truth — and the
// scheduler simply declines to hammer it.
//
// PURE, and derived entirely from existing run history: no NextRetryUtc column, no scheduler state table.
public static class TenantDatabaseBackupRetryPolicy
{
  // Whether a due operation should be suppressed this sweep because of a recent attempt.
  //
  // `null` latestRun means nothing has ever been attempted — never a reason to wait.
  //
  // FLAT INTERVALS, NOT AN ESCALATING CURVE. An earlier shape carried a consecutive-failure count and a
  // configurable history depth; production never computed either, so the escalation existed only in its own
  // unit tests while the real behaviour was a single fixed pause. Rather than add a per-database failure-
  // streak query to every sweep to make dead code true, the curve was removed. Two honest intervals — one
  // for failures, one for controlled skips — are what the scheduler actually needs, and both are
  // deployment-configurable.
  public static bool ShouldSuppress(
    TenantDatabaseBackupRunRecord? latestRun,
    DateTimeOffset nowUtc,
    TimeSpan failureRetryBackoff,
    TimeSpan skipRetryBackoff)
  {
    if (latestRun is null)
    {
      return false;
    }

    var since = latestRun.CompletedUtc ?? latestRun.StartedUtc;

    return latestRun.Status switch
    {
      // A backup is already running under this run, or the process recording it died mid-flight. Either way
      // the scheduler does not start a second one on top of it; Phase B ownership would refuse anyway, and
      // declining here avoids the pointless run record.
      TenantDatabaseBackupRunStatus.Running => nowUtc < since + skipRetryBackoff,

      TenantDatabaseBackupRunStatus.Failed => nowUtc < since + failureRetryBackoff,

      // Skips are not failures. Another worker owning the lock, an operation already running on the server,
      // or a decision another instance already satisfied all mean coordination worked exactly as designed
      // (ADR-022 §13, §14) — so the pause is short.
      TenantDatabaseBackupRunStatus.SkippedOwnershipHeld or
      TenantDatabaseBackupRunStatus.SkippedInFlightOperation => nowUtc < since + skipRetryBackoff,

      // BlockedByPolicy is not retried on a timer: it clears when policy or authority changes, not when a
      // clock advances. Eligibility filtering should mean the scheduler rarely sees one at all.
      TenantDatabaseBackupRunStatus.BlockedByPolicy => true,

      // Succeeded, Pending, VerificationFailed: due-ness already accounts for success, and the others carry
      // no retry meaning.
      _ => false
    };
  }
}
