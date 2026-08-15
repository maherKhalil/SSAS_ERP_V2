using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// Deterministic chain selection (ADR-022 §17, TS-Backup Phase D5).
//
// THE LSN VALUES BELOW ARE REAL. They are taken from an actual chain built against SQL Server 2022 — full,
// log, differential, log, full, differential, log — and read back from `msdb.dbo.backupset`. Using observed
// values rather than invented ones is the point: the relationships being asserted are SQL Server's, and a
// fixture with tidy made-up numbers could satisfy a rule that reality does not.
[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseBackupChainSelectorTests
{
  // The observed sample, preserving the exact relationships:
  //   Full1  chk 264, dbl 0,   fst 264, lst 288
  //   Log1   chk 264, dbl 264, fst 264, lst 312   <- contains Full1's end (264 <= 288 < 312)
  //   Diff1  chk 392, dbl 264, fst 392, lst 416   <- anchored to Full1 by chk
  //   Log2   chk 392, dbl 264, fst 312, lst 416
  //   Full2  chk 512, dbl 264, fst 512, lst 536
  //   Diff2  chk 616, dbl 512, fst 616, lst 640   <- anchored to Full2
  //   Log3   chk 616, dbl 512, fst 416, lst 640
  private static readonly TenantDatabaseBackupChainCandidate Full1 =
    Candidate(1, TenantDatabaseRestoreStepKind.Full, chk: 264, dbl: 0, fst: 264, lst: 288);

  private static readonly TenantDatabaseBackupChainCandidate Log1 =
    Candidate(2, TenantDatabaseRestoreStepKind.Log, chk: 264, dbl: 264, fst: 264, lst: 312);

  private static readonly TenantDatabaseBackupChainCandidate Diff1 =
    Candidate(3, TenantDatabaseRestoreStepKind.Differential, chk: 392, dbl: 264, fst: 392, lst: 416);

  private static readonly TenantDatabaseBackupChainCandidate Log2 =
    Candidate(4, TenantDatabaseRestoreStepKind.Log, chk: 392, dbl: 264, fst: 312, lst: 416);

  private static readonly TenantDatabaseBackupChainCandidate Full2 =
    Candidate(5, TenantDatabaseRestoreStepKind.Full, chk: 512, dbl: 264, fst: 512, lst: 536);

  private static readonly TenantDatabaseBackupChainCandidate Diff2 =
    Candidate(6, TenantDatabaseRestoreStepKind.Differential, chk: 616, dbl: 512, fst: 616, lst: 640);

  private static readonly TenantDatabaseBackupChainCandidate Log3 =
    Candidate(7, TenantDatabaseRestoreStepKind.Log, chk: 616, dbl: 512, fst: 416, lst: 640);

  [Fact]
  public void Level_a_selects_the_latest_full_alone()
  {
    var chain = Select([Full1, Log1, Diff1, Full2, Diff2], TenantDatabaseRestoreDepth.Full);

    Assert.Single(chain.Steps);
    Assert.Equal(TenantDatabaseRestoreStepKind.Full, chain.Steps[0].Kind);
    Assert.Equal(Full2.BackupRunId, chain.Steps[0].Artifact.BackupRunId);
  }

  // "LATEST APPLICABLE", not latest by time. Diff1 is anchored to Full1 and is not restorable onto Full2,
  // so selecting it would produce a sequence that fails at restore.
  [Fact]
  public void Level_b_selects_only_a_differential_anchored_to_the_selected_baseline()
  {
    var chain = Select([Full1, Diff1, Full2, Diff2], TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Equal(2, chain.Steps.Count);
    Assert.Equal(Full2.BackupRunId, chain.Steps[0].Artifact.BackupRunId);
    Assert.Equal(Diff2.BackupRunId, chain.Steps[1].Artifact.BackupRunId);
  }

  [Fact]
  public void Level_b_omits_a_differential_belonging_to_a_superseded_baseline()
  {
    // Only Diff1 exists, and it belongs to Full1. Against Full2 the correct answer is the baseline alone.
    var chain = Select([Full1, Diff1, Full2], TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.Single(chain.Steps);
    Assert.Equal(Full2.BackupRunId, chain.Steps[0].Artifact.BackupRunId);
  }

  // The observed sample ends with Diff2 and Log3 sharing the same last_lsn (640): taking a differential does
  // not advance the log, so the log that followed it covers the same endpoint. No log EXTENDS past the
  // differential, and the correct sequence is therefore full + differential with no tail.
  //
  // This case is kept because it is the one that corrected an assumption: an earlier version of this test
  // expected the trailing log to be selected simply because it was taken afterwards. Restoring it would add
  // nothing — the differential already carries every change through that point.
  [Fact]
  public void Level_c_stops_at_the_differential_when_no_log_extends_beyond_it()
  {
    var chain = Select(
      [Full1, Log1, Diff1, Log2, Full2, Diff2, Log3],
      TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(2, chain.Steps.Count);
    Assert.Equal(Full2.BackupRunId, chain.Steps[0].Artifact.BackupRunId);
    Assert.Equal(Diff2.BackupRunId, chain.Steps[1].Artifact.BackupRunId);
    Assert.Equal(Diff2.LastLsn, chain.RecoveryPointLsn);
  }

  // ...and when a log DOES extend beyond the differential, it is selected and sets the recovery point.
  [Fact]
  public void Level_c_selects_full_differential_and_a_log_that_extends_beyond_it()
  {
    var tail = Candidate(
      8, TenantDatabaseRestoreStepKind.Log, chk: 616, dbl: 512, fst: 416, lst: 720);

    var chain = Select(
      [Full1, Log1, Diff1, Log2, Full2, Diff2, tail],
      TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(3, chain.Steps.Count);
    Assert.Equal(Full2.BackupRunId, chain.Steps[0].Artifact.BackupRunId);
    Assert.Equal(Diff2.BackupRunId, chain.Steps[1].Artifact.BackupRunId);
    Assert.Equal(tail.BackupRunId, chain.Steps[2].Artifact.BackupRunId);
    Assert.Equal(tail.LastLsn, chain.RecoveryPointLsn);
  }

  // A DIFFERENTIAL IS NOT A PRECONDITION FOR LOG VERIFICATION (ADR-022 §17, v1.2).
  [Fact]
  public void Level_c_works_with_no_applicable_differential()
  {
    var chain = Select([Full1, Log1, Log2], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(3, chain.Steps.Count);
    Assert.Equal(TenantDatabaseRestoreStepKind.Full, chain.Steps[0].Kind);
    Assert.Equal(Log1.BackupRunId, chain.Steps[1].Artifact.BackupRunId);
    Assert.Equal(Log2.BackupRunId, chain.Steps[2].Artifact.BackupRunId);
  }

  // The first log needed is the one whose range CONTAINS the data backup's end — Full1 ends at 288 inside
  // Log1's 264..312. A rule keyed on equality with the data backup's last_lsn would select nothing here and
  // report a false chain break.
  [Fact]
  public void The_first_log_is_the_one_spanning_the_data_backups_end_not_one_starting_at_it()
  {
    var chain = Select([Full1, Log1], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(2, chain.Steps.Count);
    Assert.Equal(Log1.BackupRunId, chain.Steps[1].Artifact.BackupRunId);
  }

  // The recovery point is fixed at selection time and is the end of the selected sequence.
  [Fact]
  public void The_recovery_point_is_the_end_of_the_selected_sequence()
  {
    var chain = Select([Full1, Log1, Log2], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(Log2.LastLsn, chain.RecoveryPointLsn);
    Assert.Equal(Log2.BackupRunId, chain.RecoveryPointBackupRunId);
  }

  // Later logs do not mutate an already-selected operation: selecting from a smaller candidate set yields
  // the same sequence the operation was admitted for.
  [Fact]
  public void A_later_log_does_not_change_an_already_selected_chain()
  {
    var atSelection = Select([Full1, Log1], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);
    var withLaterLog = Select([Full1, Log1, Log2], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Equal(Log1.LastLsn, atSelection.RecoveryPointLsn);
    Assert.NotEqual(atSelection.RecoveryPointLsn, withLaterLog.RecoveryPointLsn);
  }

  // ---- Chain breaks. An external non-copy-only log takes a range the platform never recorded, leaving a
  // gap its own artifacts cannot span.

  [Fact]
  public void A_gap_in_the_log_sequence_is_a_chain_break()
  {
    // Log2 has been replaced by an external backup: the platform holds Log1 (264..312) and a later log
    // starting at 416, with nothing bridging 312..416.
    var orphanedTail = Candidate(
      9, TenantDatabaseRestoreStepKind.Log, chk: 616, dbl: 264, fst: 416, lst: 640);

    var result = TenantDatabaseBackupChainSelector.Select(
      [Full1, Log1, orphanedTail], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreChainBroken.Code, result.Error.Code);
  }

  // A log that begins after the baseline's end with nothing covering the interval is the same break, caught
  // before any log is selected at all.
  [Fact]
  public void A_log_sequence_that_never_reaches_the_baseline_is_a_chain_break()
  {
    var detached = Candidate(
      9, TenantDatabaseRestoreStepKind.Log, chk: 616, dbl: 512, fst: 900, lst: 950);

    var result = TenantDatabaseBackupChainSelector.Select(
      [Full1, detached], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreChainBroken.Code, result.Error.Code);
  }

  // NEVER A SILENT DOWNGRADE. A broken chain must not quietly become a full-only restore that still claims
  // the requested depth (ADR-022 §17, compliance rule 37).
  [Fact]
  public void A_broken_chain_is_never_downgraded_to_a_shallower_success()
  {
    var orphanedTail = Candidate(
      9, TenantDatabaseRestoreStepKind.Log, chk: 616, dbl: 264, fst: 416, lst: 640);

    var result = TenantDatabaseBackupChainSelector.Select(
      [Full1, Log1, orphanedTail], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.True(result.IsFailure);
  }

  // A log-policy database whose newest backup IS the data backup has no tail yet. That is not a break — the
  // data backup alone reaches the same recovery point.
  [Fact]
  public void A_chain_with_no_logs_after_the_baseline_is_not_a_break()
  {
    var chain = Select([Full1], TenantDatabaseRestoreDepth.FullWithDifferentialAndLog);

    Assert.Single(chain.Steps);
    Assert.Equal(Full1.LastLsn, chain.RecoveryPointLsn);
  }

  [Fact]
  public void No_full_baseline_is_reported_distinctly_from_a_break()
  {
    var result = TenantDatabaseBackupChainSelector.Select(
      [Log1, Log2], TenantDatabaseRestoreDepth.Full);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreChainBaselineMissing.Code, result.Error.Code);
  }

  // Only platform-managed artifacts are candidates at all — the selector's input IS the platform's own
  // successful runs, so an external backup has no way to enter the sequence.
  [Fact]
  public void Selection_carries_the_trusted_destination_key_and_artifact_reference()
  {
    var chain = Select([Full1], TenantDatabaseRestoreDepth.Full);

    Assert.Equal("primary-destination", chain.Steps[0].Artifact.DestinationKey);
    Assert.Equal("1_Full_20260815T000000Z_1.bak", chain.Steps[0].Artifact.ArtifactReference);
  }

  private static TenantDatabaseRestoreChain Select(
    IReadOnlyList<TenantDatabaseBackupChainCandidate> candidates,
    TenantDatabaseRestoreDepth depth)
  {
    var result = TenantDatabaseBackupChainSelector.Select(candidates, depth);
    Assert.True(result.IsSuccess);
    return result.Value;
  }

  private static TenantDatabaseBackupChainCandidate Candidate(
    long id,
    TenantDatabaseRestoreStepKind operation,
    decimal chk,
    decimal dbl,
    decimal fst,
    decimal lst) =>
    new(id, operation, "primary-destination",
      $"1_{operation}_20260815T000000Z_{id}.bak",
      chk, dbl, fst, lst);
}
