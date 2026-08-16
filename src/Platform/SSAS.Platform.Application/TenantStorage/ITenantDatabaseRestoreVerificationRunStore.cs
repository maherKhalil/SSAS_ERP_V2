using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// The WRITE path for restore-verification operations (ADR-022 §17, TS-Backup Phase D).
//
// INTENT-SPECIFIC METHODS, following the backup run store's precedent. There is no "set status to whatever":
// admission, beginning a restore, succeeding, failing, abandoning as unavailable and recording cleanup mean
// materially different things, and the two most worth protecting — success, and a cleanup failure that must
// NOT disturb a proven restore — must not be interchangeable.
//
// Every method is a SHORT write. No Platform transaction is held across a restore, which can run for hours.
public interface ITenantDatabaseRestoreVerificationRunStore
{
  // Execution always starts by loading the EXACT durable operation it was handed. This is intentionally
  // not a "find active run" query: an old worker must never substitute a newer operation for its own.
  Task<TenantDatabaseRestoreVerificationRunRecord?> FindAsync(
    long verificationRunId,
    CancellationToken cancellationToken = default);

  // ADMISSION — the serialising event (ADR-022 compliance rule 43).
  //
  // Returns the admitted run, or a failure when another application instance already holds the effective
  // verification for this database, or when the database is no longer due. Both are ORDINARY OUTCOMES rather
  // than errors: the first means the invariant worked, and the second means a stale decision was caught.
  Task<Result<long>> TryAdmitAsync(
    TenantDatabaseRestoreVerificationAdmissionRequest request,
    CancellationToken cancellationToken = default);

  // Records the database this run is about to create, and moves it to Restoring. Called BEFORE the restore,
  // never after.
  Task<Result> BeginRestoreAsync(
    long verificationRunId,
    string verificationDatabaseName,
    string actor,
    CancellationToken cancellationToken = default);

  Task<Result> MarkSucceededAsync(
    long verificationRunId,
    string actor,
    CancellationToken cancellationToken = default);

  // The authoritative D7 success write. The verification run and the exact admitted full baseline become
  // successful evidence in one short Platform transaction. Aggregate readiness is projected afterwards,
  // so a stale admission decision can already observe the durable Succeeded run even if that projection is
  // temporarily behind.
  Task<Result<DateTimeOffset>> MarkSucceededAndRecordEvidenceAsync(
    long verificationRunId,
    long sourceBackupRunId,
    string actor,
    CancellationToken cancellationToken = default);

  Task<Result> MarkFailedAsync(
    long verificationRunId,
    string? errorSummary,
    string actor,
    CancellationToken cancellationToken = default);

  // The attempt could not begin or complete for reasons independent of the artifacts. Separate from failure
  // so a verification-host outage never degrades readiness (ADR-022 §17, v1.2).
  Task<Result> MarkInfrastructureUnavailableAsync(
    long verificationRunId,
    string? reasonSummary,
    string actor,
    CancellationToken cancellationToken = default);

  // Disposal outcome, recorded independently. Cannot change the verification result — the aggregate makes
  // that inexpressible rather than merely discouraged.
  Task<Result> RecordCleanupAsync(
    long verificationRunId,
    TenantDatabaseVerificationCleanupState state,
    string? errorSummary,
    string actor,
    CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseRestoreVerificationRunRecord(
  long VerificationRunId,
  long TenantDatabaseId,
  long SourceBackupRunId,
  TenantDatabaseRestoreDepth Depth,
  string RestoreServerKey,
  TenantDatabaseRestoreVerificationStatus Status,
  string? VerificationDatabaseName,
  DateTimeOffset StartedUtc,
  DateTimeOffset? CompletedUtc);

// What admission needs to decide whether this instance may take the work.
//
// THIS RECORD IS A SNAPSHOT OF THE DUE DECISION, and admission's job is to detect that the snapshot has gone
// stale. Two facts identify the due state together, and BOTH are required:
//
//   SourceBackupRunId — the baseline this verification would restore. A newer full backup means the chain
//                       being verified has moved on.
//
//   ExpectedPreviousSuccessfulVerificationRunId — the successful restore verification that existed WHEN the
//                       due decision was made, or null if there had never been one. If a newer successful
//                       verification exists by the time this reaches admission, someone else already
//                       satisfied this obligation and this decision is answering a stale question.
//
// The second is what closes the sequential duplicate. The baseline alone cannot: a completed verification
// does not change the baseline, so a stale worker whose verification has already been performed by another
// instance would otherwise pass a baseline-only check and repeat the work.
//
// It is deliberately an ANCHOR rather than a rule that a baseline may be verified only once. The same full
// baseline legitimately needs verifying again when the policy's interval expires and no newer full exists —
// that is a NEW due state, anchored to the verification that has since gone stale, and it must be admissible.
public sealed record TenantDatabaseRestoreVerificationAdmissionRequest(
  long TenantDatabaseId,
  long SourceBackupRunId,
  long? ExpectedPreviousSuccessfulVerificationRunId,
  TenantDatabaseRestoreDepth Depth,
  string RestoreServerKey,
  string Actor);
