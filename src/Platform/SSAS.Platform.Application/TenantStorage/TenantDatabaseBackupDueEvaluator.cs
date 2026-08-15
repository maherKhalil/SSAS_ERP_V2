using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Decides WHICH backup operation, if any, a physical database is due for (ADR-022 §13, TS-Backup Phase C).
//
// PURE. No DbContext, no SQL, no clock of its own — the caller supplies `nowUtc`. That is what makes the
// scheduling rules testable without a database, and it is why every interesting due/precedence question in
// this slice is answered by a unit test rather than by a real backup.
//
// The evaluator answers only the platform's OWN schedule. Whether SQL Server's chain is actually valid — a
// differential base that exists, a log chain that is unbroken — is the provider's question, established from
// msdb evidence at execution time (ADR-022 §14). An externally taken backup can change SQL Server's chain
// state without discharging the platform's scheduling obligation, so nothing here reads external history.
public static class TenantDatabaseBackupDueEvaluator
{
  // Whether the scheduler may dispatch for this database at all.
  //
  // Mirrors the executor's authority checks rather than replacing them. The scheduler filters early so a
  // forbidden database does not accumulate a BlockedByPolicy run on every sweep; the executor still refuses
  // independently, because eligibility computed from a projection is advisory and the policy may have
  // changed since (ADR-022 compliance rules 5 and 26).
  public static bool IsEligible(TenantDatabaseBackupDueCandidate candidate)
  {
    ArgumentNullException.ThrowIfNull(candidate);

    return candidate.PolicyEnabled &&
      candidate.ManagementMode == TenantDatabaseBackupManagementMode.AutomaticByPlatform &&
      candidate.HostingMode == TenantDatabaseHostingMode.PlatformManaged &&
      candidate.ProvisioningStatus == TenantDatabaseProvisioningStatus.Ready;
  }

  // The single operation to dispatch this sweep, or null when nothing is due.
  //
  // PRECEDENCE IS ARCHITECTURE (ADR-022 §13, compliance rule 31): transaction log, then full, then
  // differential — and exactly ONE per database per sweep. The remaining operations are reconsidered on a
  // later sweep, against the timestamps the completed one will by then have persisted.
  //
  // Log first because it protects the recovery POINT: delaying it widens the window of unrecoverable data,
  // and it should not queue behind a long full. Full before differential because a full resets the
  // differential base, so a differential taken immediately before a due full is work the full makes
  // redundant.
  public static TenantDatabaseBackupOperation? SelectDueOperation(
    TenantDatabaseBackupDueCandidate candidate,
    DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(candidate);

    if (!IsEligible(candidate))
    {
      return null;
    }

    if (IsTransactionLogDue(candidate, nowUtc))
    {
      return TenantDatabaseBackupOperation.SqlServerTransactionLog();
    }

    if (IsFullDue(candidate, nowUtc))
    {
      return TenantDatabaseBackupOperation.SqlServerFull();
    }

    return IsDifferentialDue(candidate, nowUtc)
      ? TenantDatabaseBackupOperation.SqlServerDifferential()
      : null;
  }

  // A full is due when the policy schedules one and none has ever succeeded, or the interval has elapsed.
  //
  // A NULL interval means the policy does not schedule this operation — NOT that it is overdue. The
  // distinction matters: all three interval columns are nullable, and reading "unset" as "due immediately"
  // would turn an unconfigured cadence into a continuous backup loop.
  public static bool IsFullDue(TenantDatabaseBackupDueCandidate candidate, DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(candidate);

    return IsElapsed(
      candidate.FullBackupIntervalMinutes,
      candidate.LastSuccessfulFullBackupUtc,
      nowUtc,
      dueWhenNeverRun: true);
  }

  // A differential needs a full to be differential FROM. Without an observed full baseline the platform has
  // nothing to anchor to, so it schedules none and lets the full become due instead.
  //
  // Where no differential has yet succeeded, the baseline full is the anchor — the first differential falls
  // due one interval after the full it depends on, not immediately.
  public static bool IsDifferentialDue(TenantDatabaseBackupDueCandidate candidate, DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(candidate);

    if (candidate.LastSuccessfulFullBackupUtc is null)
    {
      return false;
    }

    return IsElapsed(
      candidate.DifferentialBackupIntervalMinutes,
      candidate.LastSuccessfulDifferentialBackupUtc ?? candidate.LastSuccessfulFullBackupUtc,
      nowUtc,
      dueWhenNeverRun: false);
  }

  // A log backup has the same baseline requirement: a database in FULL recovery with no full backup is
  // pseudo-simple and cannot produce a usable log chain. The provider enforces this again from SQL Server's
  // own evidence; scheduling it here would only manufacture a guaranteed block.
  public static bool IsTransactionLogDue(TenantDatabaseBackupDueCandidate candidate, DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(candidate);

    if (candidate.LastSuccessfulFullBackupUtc is null)
    {
      return false;
    }

    return IsElapsed(
      candidate.TransactionLogBackupIntervalMinutes,
      candidate.LastSuccessfulLogBackupUtc ?? candidate.LastSuccessfulFullBackupUtc,
      nowUtc,
      dueWhenNeverRun: false);
  }

  private static bool IsElapsed(
    int? intervalMinutes,
    DateTimeOffset? anchorUtc,
    DateTimeOffset nowUtc,
    bool dueWhenNeverRun)
  {
    // Not scheduled at all.
    if (intervalMinutes is not > 0)
    {
      return false;
    }

    if (anchorUtc is null)
    {
      return dueWhenNeverRun;
    }

    return nowUtc >= anchorUtc.Value.AddMinutes(intervalMinutes.Value);
  }
}
