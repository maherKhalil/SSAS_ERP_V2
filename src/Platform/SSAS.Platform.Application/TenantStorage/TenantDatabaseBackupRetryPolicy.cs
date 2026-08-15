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
  // Skips are not failures. Another worker owning the lock, or an operation already running on the server,
  // both mean coordination worked exactly as designed (ADR-022 §14) — so the pause is short, just long
  // enough to avoid spinning against a backup that is genuinely in progress.
  public static readonly TimeSpan SkipBackoff = TimeSpan.FromMinutes(1);

  // Whether a due operation should be suppressed this sweep because of a recent attempt.
  //
  // `null` latestRun means nothing has ever been attempted — never a reason to wait.
  public static bool ShouldSuppress(
    TenantDatabaseBackupRunRecord? latestRun,
    DateTimeOffset nowUtc,
    TimeSpan failureInitialBackoff,
    TimeSpan failureMaximumBackoff,
    int consecutiveFailures = 1)
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
      TenantDatabaseBackupRunStatus.Running => nowUtc < since + SkipBackoff,

      TenantDatabaseBackupRunStatus.Failed =>
        nowUtc < since + BackoffFor(consecutiveFailures, failureInitialBackoff, failureMaximumBackoff),

      TenantDatabaseBackupRunStatus.SkippedOwnershipHeld or
      TenantDatabaseBackupRunStatus.SkippedInFlightOperation => nowUtc < since + SkipBackoff,

      // BlockedByPolicy is not retried on a timer: it clears when policy or authority changes, not when a
      // clock advances. Eligibility filtering should mean the scheduler rarely sees one at all.
      TenantDatabaseBackupRunStatus.BlockedByPolicy => true,

      // Succeeded, Pending, VerificationFailed: due-ness already accounts for success, and the others carry
      // no retry meaning.
      _ => false
    };
  }

  // Escalating, capped. Deliberately simple arithmetic rather than a retry framework — the failure modes
  // this guards against are "a database is unreachable for an hour" and "a destination is misconfigured",
  // neither of which is helped by a sophisticated curve.
  public static TimeSpan BackoffFor(
    int consecutiveFailures,
    TimeSpan initial,
    TimeSpan maximum)
  {
    if (consecutiveFailures <= 1)
    {
      return initial;
    }

    // Doubling, but computed in ticks with an early exit so a long failure streak cannot overflow.
    var backoff = initial;
    for (var attempt = 1; attempt < consecutiveFailures; attempt++)
    {
      backoff += backoff;
      if (backoff >= maximum)
      {
        return maximum;
      }
    }

    return backoff > maximum ? maximum : backoff;
  }
}
