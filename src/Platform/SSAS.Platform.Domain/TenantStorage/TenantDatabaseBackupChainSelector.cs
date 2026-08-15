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
      return Result.Success(Complete(steps, baseline, TenantDatabaseRestoreDepth.Full));
    }

    // THE LATEST APPLICABLE DIFFERENTIAL — applicable meaning anchored to THIS baseline, never merely the
    // most recent one. A differential taken against a superseded full is not restorable onto this one, so
    // selecting by time alone would produce a sequence that fails at restore.
    var differential = candidates
      .Where(candidate => candidate.Operation == TenantDatabaseRestoreStepKind.Differential &&
        candidate.DatabaseBackupLsn == baseline.CheckpointLsn)
      .OrderByDescending(candidate => candidate.LastLsn)
      .FirstOrDefault();

    // AN ORPHANED DIFFERENTIAL IS A CHAIN BREAK, NOT A QUIETER RESULT.
    //
    // These two situations produce the same STEPS and must not produce the same VERDICT:
    //
    //   * no platform differential has ever been taken — the full alone genuinely is the whole chain; and
    //   * a platform differential exists, but an external non-copy-only full reset the differential base, so
    //     the platform's own differential is anchored to an artifact the platform neither recorded nor can
    //     locate.
    //
    // In the second case the differential path CANNOT be exercised from platform-owned artifacts. Returning a
    // full-only chain there would report a shallower restore while the caller believes the depth its policy
    // claims was verified — the silent downgrade ADR-022 §17 (v1.2) prohibits and compliance rule 37 names.
    if (differential is null && HasOrphanedDifferential(candidates, baseline))
    {
      return Result.Failure<TenantDatabaseRestoreChain>(TenantStorageErrors.RestoreChainBroken);
    }

    if (differential is not null)
    {
      steps.Add(new TenantDatabaseRestoreChainStep(
        TenantDatabaseRestoreStepKind.Differential, differential));
    }

    // A DIFFERENTIAL IS NOT A PRECONDITION FOR LOG VERIFICATION (ADR-022 §17, v1.2). Where none has ever been
    // taken, Level B is the baseline alone and Level C is the baseline followed by logs — but the ACHIEVED
    // depth says so rather than letting the request imply it.
    if (depth == TenantDatabaseRestoreDepth.FullWithDifferential)
    {
      return Result.Success(Complete(
        steps,
        differential ?? baseline,
        differential is null ? TenantDatabaseRestoreDepth.Full : TenantDatabaseRestoreDepth.FullWithDifferential));
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

    // ACHIEVED DEPTH REFLECTS WHAT THE SEQUENCE ACTUALLY EXERCISES. A chain with no log segment does not
    // exercise the log recovery path, however it was requested: where the latest platform backup IS the data
    // backup there is no tail to restore, and saying so is the difference between "nothing to verify beyond
    // this point" and "the log path was verified".
    var achieved = logs.Value.Count > 0
      ? TenantDatabaseRestoreDepth.FullWithDifferentialAndLog
      : differential is null
        ? TenantDatabaseRestoreDepth.Full
        : TenantDatabaseRestoreDepth.FullWithDifferential;

    return Result.Success(Complete(steps, recoveryPoint, achieved));
  }

  // Does a platform differential exist that CANNOT be applied to the selected baseline?
  //
  // Distinguishes "none was ever taken" from "one exists but its base is not ours". Only the latter means the
  // differential path is unverifiable from platform-owned artifacts.
  private static bool HasOrphanedDifferential(
    IReadOnlyList<TenantDatabaseBackupChainCandidate> candidates,
    TenantDatabaseBackupChainCandidate baseline) =>
    candidates.Any(candidate =>
      candidate.Operation == TenantDatabaseRestoreStepKind.Differential &&
      candidate.DatabaseBackupLsn != baseline.CheckpointLsn &&
      // Only differentials taken AFTER this baseline matter. One belonging to an older full is ordinary
      // history that a newer baseline has legitimately superseded, not evidence of a broken chain.
      candidate.LastLsn > baseline.LastLsn);

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
    TenantDatabaseBackupChainCandidate recoveryPoint,
    TenantDatabaseRestoreDepth achievedDepth) =>
    new(steps, recoveryPoint.LastLsn, recoveryPoint.BackupRunId, achievedDepth);
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
//
// `AchievedDepth` IS THE HALF THAT STOPS A SHALLOWER RESTORE CLAIMING A DEEPER GUARANTEE. It states what the
// selected sequence actually exercises, which is not always what was requested: a Level C request against a
// database whose newest backup is its full yields a full-only sequence, and the caller must be able to see
// that rather than infer verification of a log path that has no segments to restore.
//
// The rule downstream is a comparison, not a boolean: a verification counts at the requested depth only when
// `AchievedDepth >= RequestedDepth`. Reusing the existing depth vocabulary keeps that comparison meaningful
// and avoids inventing a second way to describe the same three levels.
public sealed record TenantDatabaseRestoreChain(
  IReadOnlyList<TenantDatabaseRestoreChainStep> Steps,
  decimal RecoveryPointLsn,
  long RecoveryPointBackupRunId,
  TenantDatabaseRestoreDepth AchievedDepth);
