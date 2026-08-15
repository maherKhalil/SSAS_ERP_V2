using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// Deterministic selection of the artifacts a restore verification will restore (ADR-022 §17, v1.2).
//
// PURE AND IN THE DOMAIN. Which artifacts constitute a recoverable sequence is a durability question, not a
// provider detail, and keeping it pure is what lets every chain shape be tested without a server.
//
// THE LSN RELATIONSHIPS HERE WERE ESTABLISHED EMPIRICALLY, not from memory. A real chain — full, log,
// differential, log, full, differential, log — was taken against SQL Server 2022 and its `msdb.dbo.backupset`
// rows read directly. What that showed:
//
//   * A DIFFERENTIAL's `database_backup_lsn` equals its base FULL's `checkpoint_lsn`. Two differentials in
//     the sample anchored to two different fulls, and each matched its own base rather than the latest one.
//     This is what makes "latest applicable" decidable rather than a guess about ordering.
//
//   * LOGS CHAIN NOSE-TO-TAIL: each log's `first_lsn` equals the previous log's `last_lsn`. A gap between
//     them is a missing segment, which is the observable form of a chain break.
//
//   * THE FIRST LOG NEEDED AFTER A DATA BACKUP is the one whose range CONTAINS that backup's `last_lsn` —
//     `first_lsn <= dataBackup.last_lsn < last_lsn`. It is not "the next log by time", and it is not
//     necessarily a log whose `first_lsn` equals the data backup's `last_lsn`: in the sample, the full ended
//     at ...288 while the log covering it spanned ...264 to ...312.
//
// ADR-022 v1.2 deliberately declines to freeze a formula, so these are treated as OBSERVED SQL SERVER
// BEHAVIOUR that the real-SQL restore tests re-establish, rather than as an invented rule.
public static class TenantDatabaseBackupChainSelector
{
  // Selects the restore sequence for one verification, or explains why no complete sequence exists.
  //
  // ONLY PLATFORM-MANAGED ARTIFACTS ARE CANDIDATES. The caller supplies successful platform backup runs;
  // externally created backups are never inputs, so they cannot be selected however convenient they look
  // (ADR-022 §17, compliance rule 37). Where external activity has left the platform's own artifacts unable
  // to form the required sequence, that is a CHAIN BREAK and is reported as one — never quietly downgraded
  // to a shallower restore that still claims the requested depth.
  public static Result<TenantDatabaseRestoreChain> Select(
    IReadOnlyList<TenantDatabaseBackupChainCandidate> candidates,
    TenantDatabaseRestoreDepth depth)
  {
    ArgumentNullException.ThrowIfNull(candidates);

    // THE BASELINE: the most recent successful platform full. Selected by backup-set identity order rather
    // than by wall-clock time, because time is a property of when a job ran and LSN order is a property of
    // the chain itself.
    var baseline = candidates
      .Where(candidate => candidate.Operation == TenantDatabaseRestoreStepKind.Full)
      .OrderByDescending(candidate => candidate.LastLsn)
      .FirstOrDefault();

    if (baseline is null)
    {
      return Result.Failure<TenantDatabaseRestoreChain>(TenantStorageErrors.RestoreChainBaselineMissing);
    }

    var steps = new List<TenantDatabaseRestoreChainStep>
    {
      new(TenantDatabaseRestoreStepKind.Full, baseline)
    };

    if (depth == TenantDatabaseRestoreDepth.Full)
    {
      return Result.Success(Complete(steps, baseline));
    }

    // THE LATEST APPLICABLE DIFFERENTIAL — applicable meaning anchored to THIS baseline, never merely the
    // most recent one. A differential taken against a superseded full is not restorable onto this one, so
    // selecting by time alone would produce a sequence that fails at restore.
    var differential = candidates
      .Where(candidate => candidate.Operation == TenantDatabaseRestoreStepKind.Differential &&
        candidate.DatabaseBackupLsn == baseline.CheckpointLsn)
      .OrderByDescending(candidate => candidate.LastLsn)
      .FirstOrDefault();

    if (differential is not null)
    {
      steps.Add(new TenantDatabaseRestoreChainStep(
        TenantDatabaseRestoreStepKind.Differential, differential));
    }

    // A DIFFERENTIAL IS NOT A PRECONDITION FOR LOG VERIFICATION (ADR-022 §17, v1.2). Where none applies,
    // Level B is the baseline alone and Level C is the baseline followed by logs.
    if (depth == TenantDatabaseRestoreDepth.FullWithDifferential)
    {
      return Result.Success(Complete(steps, differential ?? baseline));
    }

    var lastDataStep = differential ?? baseline;
    var logs = SelectLogs(candidates, lastDataStep);
    if (logs.IsFailure)
    {
      return Result.Failure<TenantDatabaseRestoreChain>(logs.Error);
    }

    foreach (var log in logs.Value)
    {
      steps.Add(new TenantDatabaseRestoreChainStep(TenantDatabaseRestoreStepKind.Log, log));
    }

    // THE RECOVERY POINT IS FIXED AT SELECTION TIME (ADR-022 §17, v1.2). It is the end of the contiguous log
    // sequence available now — not a moving target that grows as later logs arrive, which is what keeps a
    // verification finite on a database whose log cadence is measured in minutes.
    var recoveryPoint = logs.Value.Count > 0 ? logs.Value[^1] : lastDataStep;
    return Result.Success(Complete(steps, recoveryPoint));
  }

  // The contiguous log run from the last restored data backup through the latest log available.
  private static Result<IReadOnlyList<TenantDatabaseBackupChainCandidate>> SelectLogs(
    IReadOnlyList<TenantDatabaseBackupChainCandidate> candidates,
    TenantDatabaseBackupChainCandidate lastDataStep)
  {
    var ordered = candidates
      .Where(candidate => candidate.Operation == TenantDatabaseRestoreStepKind.Log)
      .OrderBy(candidate => candidate.FirstLsn)
      .ToList();

    // The first log needed is the one whose range CONTAINS the data backup's end. Established empirically:
    // the sample's full ended at ...288 inside a log spanning ...264 to ...312, so a rule keyed on equality
    // with the data backup's last_lsn would have selected nothing and reported a false break.
    var index = ordered.FindIndex(log =>
      log.FirstLsn <= lastDataStep.LastLsn && log.LastLsn > lastDataStep.LastLsn);

    if (index < 0)
    {
      // Nothing continues the chain past this data backup. That is not necessarily a break: a log-policy
      // database whose latest backup IS the data backup simply has no tail yet, and restoring the data
      // backup alone reaches the same recovery point.
      return ordered.Exists(log => log.FirstLsn > lastDataStep.LastLsn)
        // ...but a log that starts AFTER the data backup's end, with nothing bridging the gap, is a genuine
        // missing segment: the platform cannot reconstruct the required path from its own artifacts.
        ? Result.Failure<IReadOnlyList<TenantDatabaseBackupChainCandidate>>(
          TenantStorageErrors.RestoreChainBroken)
        : Result.Success<IReadOnlyList<TenantDatabaseBackupChainCandidate>>([]);
    }

    var selected = new List<TenantDatabaseBackupChainCandidate> { ordered[index] };

    // NOSE-TO-TAIL CONTIGUITY, verified rather than assumed at restore time. Each subsequent log must begin
    // exactly where the previous ended; the first discontinuity terminates the chain.
    for (var next = index + 1; next < ordered.Count; next++)
    {
      var previous = selected[^1];
      if (ordered[next].FirstLsn == previous.LastLsn)
      {
        selected.Add(ordered[next]);
        continue;
      }

      // A later log exists but does not continue from here — an external non-copy-only log backup took the
      // intervening range, and the platform's own artifacts can no longer span it. Reported as a break so
      // readiness degrades, never skipped to reach the newest log (ADR-022 §17, compliance rule 37).
      if (ordered[next].FirstLsn > previous.LastLsn)
      {
        return Result.Failure<IReadOnlyList<TenantDatabaseBackupChainCandidate>>(
          TenantStorageErrors.RestoreChainBroken);
      }
    }

    return Result.Success<IReadOnlyList<TenantDatabaseBackupChainCandidate>>(selected);
  }

  private static TenantDatabaseRestoreChain Complete(
    List<TenantDatabaseRestoreChainStep> steps,
    TenantDatabaseBackupChainCandidate recoveryPoint) =>
    new(steps, recoveryPoint.LastLsn, recoveryPoint.BackupRunId);
}

// The ROLE an artifact plays in a restore sequence: the base image, a cumulative delta on that base, or a
// log segment continuing from it.
//
// DELIBERATELY NOT SQL SERVER'S OPERATION VOCABULARY. `Full`/`Differential`/`TransactionLog` are SQL Server's
// names and stay provider-scoped in `TenantDatabaseBackupOperation` (ADR-022 §10, compliance rule 22); an
// enum in the domain repeating all three would be exactly the universal backup-type enum that rule forbids,
// and the architecture guard catches it. What the selector reasons about is sequence position, which is a
// genuine cross-provider concept — the caller maps its provider's operation codes onto these roles.
public enum TenantDatabaseRestoreStepKind
{
  Full = 1,

  Differential = 2,

  Log = 3
}

// One PLATFORM-MANAGED successful backup run, as a chain candidate.
//
// Carries the trusted destination key and the safe artifact reference rather than a resolved path: the
// physical location is rebuilt in Infrastructure from configuration, so nothing here can direct a restore at
// a caller-chosen file (ADR-022 §11, §17).
public sealed record TenantDatabaseBackupChainCandidate(
  long BackupRunId,
  TenantDatabaseRestoreStepKind Operation,
  string? DestinationKey,
  string? ArtifactReference,
  decimal CheckpointLsn,
  decimal DatabaseBackupLsn,
  decimal FirstLsn,
  decimal LastLsn);

public sealed record TenantDatabaseRestoreChainStep(
  TenantDatabaseRestoreStepKind Kind,
  TenantDatabaseBackupChainCandidate Artifact);

// The selected sequence, fixed at selection time.
//
// `RecoveryPointLsn` and `RecoveryPointBackupRunId` name the position this verification will actually reach,
// so an operation records what it proved rather than what was available when it finished.
public sealed record TenantDatabaseRestoreChain(
  IReadOnlyList<TenantDatabaseRestoreChainStep> Steps,
  decimal RecoveryPointLsn,
  long RecoveryPointBackupRunId);
