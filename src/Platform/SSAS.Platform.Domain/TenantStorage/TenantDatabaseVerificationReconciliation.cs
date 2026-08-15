using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// What to do about a verification run that is still ACTIVE but may have been abandoned (ADR-022 §17, LOW-C).
//
// THE PROBLEM THIS EXISTS FOR: admission is guarded by a filtered unique index over `Admitted` and
// `Restoring`. A process that dies between admission and completion leaves that slot occupied forever, and
// no further verification of that database can ever be admitted. Closing it is a prerequisite for enabling
// scheduling — the alternative, weakening the unique index, would give back the duplicate protection the
// index exists to provide.
//
// AGE ALONE IS NEVER SUFFICIENT, and this is the whole design constraint. A legitimate restore of a large
// database can run for hours; releasing a slot because a row "looks old" would abandon work that is still
// in progress and then permit a second restore alongside it. Every decision here therefore combines the
// durable record with AUTHORITATIVE SERVER EVIDENCE, and the default when evidence is missing or ambiguous
// is to leave the run alone.
public static class TenantDatabaseVerificationReconciliation
{
  public static TenantDatabaseVerificationReconciliationDecision Decide(
    TenantDatabaseVerificationReconciliationInputs inputs)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    // Only active runs are candidates. A terminal run holds no slot.
    if (inputs.Status is not (TenantDatabaseRestoreVerificationStatus.Admitted or
      TenantDatabaseRestoreVerificationStatus.Restoring))
    {
      return TenantDatabaseVerificationReconciliationDecision.LeaveAlone;
    }

    // AUTHORITATIVE EVIDENCE FIRST, before anything about age is considered. A restore that SQL Server
    // reports as running is running, however long it has taken and whatever the platform's record suggests.
    if (inputs.RestoreIsActiveOnServer)
    {
      return TenantDatabaseVerificationReconciliationDecision.LeaveAlone;
    }

    // Could not establish server state — the verification host is unreachable, or visibility was refused.
    // ABSENCE OF EVIDENCE IS NOT EVIDENCE OF ABSENCE: an unreachable host says nothing about whether a
    // restore is running on it, and treating that as "abandoned" would release a slot next to live work.
    if (!inputs.ServerStateObserved)
    {
      return TenantDatabaseVerificationReconciliationDecision.LeaveAlone;
    }

    // The grace period is the LAST condition, not the first: it only decides how long to wait after the
    // evidence already says nothing is running.
    if (inputs.Age < inputs.GracePeriod)
    {
      return TenantDatabaseVerificationReconciliationDecision.LeaveAlone;
    }

    // ADMITTED, never started. No database was created, so there is nothing to dispose of and nothing to
    // correlate — the run simply never began.
    if (inputs.Status == TenantDatabaseRestoreVerificationStatus.Admitted)
    {
      return inputs.VerificationDatabaseExists
        // A database exists for a run that never recorded starting one. The record and the server disagree,
        // and the safe response to disagreement is to report rather than to act.
        ? TenantDatabaseVerificationReconciliationDecision.ReportInconsistent
        : TenantDatabaseVerificationReconciliationDecision.ReleaseAbandoned;
    }

    // RESTORING, with no restore running. The process died mid-flight.
    return inputs.VerificationDatabaseExists
      // A database was created and outlived its process. The run is abandoned AND an orphan exists to be
      // disposed of — which this slice records rather than drops, because the destructive-permission model
      // is not yet proven.
      ? TenantDatabaseVerificationReconciliationDecision.ReleaseAbandonedWithOrphan
      : TenantDatabaseVerificationReconciliationDecision.ReleaseAbandoned;
  }
}

// The facts a reconciliation decision requires. A record rather than loose parameters, so adding a condition
// later is a compile error at every call site instead of a silently weaker rule.
//
// `ServerStateObserved` is separate from `RestoreIsActiveOnServer` on purpose: "no restore is running" and
// "we could not tell" must never collapse into the same value, because they lead to opposite decisions.
public sealed record TenantDatabaseVerificationReconciliationInputs(
  TenantDatabaseRestoreVerificationStatus Status,
  bool ServerStateObserved,
  bool RestoreIsActiveOnServer,
  bool VerificationDatabaseExists,
  TimeSpan Age,
  TimeSpan GracePeriod);

public enum TenantDatabaseVerificationReconciliationDecision
{
  // Still running, still inside grace, or state could not be established. The default.
  LeaveAlone = 1,

  // Terminally abandoned, nothing left behind. The admission slot may be released.
  ReleaseAbandoned = 2,

  // Terminally abandoned, and a verification database outlived it. The slot may be released; the database
  // is surfaced as an orphan rather than removed here.
  ReleaseAbandonedWithOrphan = 3,

  // The durable record and the server disagree in a way no automated action should resolve.
  ReportInconsistent = 4
}
