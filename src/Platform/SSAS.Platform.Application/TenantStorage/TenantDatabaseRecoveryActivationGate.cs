using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// THE RECOVERY ACTIVATION GATE (ADR-017 cutover ordering, ADR-022 §18, TS-Storage Phase E).
//
// WHERE THIS SITS IN A CUTOVER, and it is one specific place:
//
//     freeze → copy → validate → **recovery activation gate** → routing flip / RoutingVersion → invalidation
//
// AFTER validation, because there is no point asking whether a target is recoverable before establishing
// that it is correct. BEFORE the routing flip, because the flip is the moment tenant writes begin landing on
// the new database, and a write that lands on an unrecoverable database cannot be taken back. This type
// grants no authority of its own: it neither freezes, copies, validates, flips routing nor invalidates
// anything, and adding it does not make a target writable one step earlier than the existing architecture
// already permits.
public interface ITenantDatabaseRecoveryActivationGate
{
  // Success means activation is authorised. Failure carries WHY, because each refusal sends an operator to a
  // different remedy.
  Task<Result> AuthorizeActivationAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);

  // The same decision, undecorated, for callers that need to report or display the precise verdict rather
  // than act on it.
  Task<Result<TenantDatabaseRecoveryActivationDecision>> EvaluateAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseRecoveryActivationGate(
  ITenantDatabaseRecoveryActivationReadRepository evidenceReads,
  IDateTimeProvider clock) : ITenantDatabaseRecoveryActivationGate
{
  public async Task<Result> AuthorizeActivationAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default)
  {
    var decision = await EvaluateAsync(tenantDatabaseId, cancellationToken);
    if (decision.IsFailure)
    {
      return Result.Failure(decision.Error);
    }

    return decision.Value == TenantDatabaseRecoveryActivationDecision.Allowed
      ? Result.Success()
      : Result.Failure(ErrorFor(decision.Value));
  }

  public async Task<Result<TenantDatabaseRecoveryActivationDecision>> EvaluateAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default)
  {
    if (tenantDatabaseId <= 0)
    {
      return Result.Failure<TenantDatabaseRecoveryActivationDecision>(
        TenantStorageErrors.RecoveryActivationEvidenceUnavailable);
    }

    var evidence = await evidenceReads.FindActivationEvidenceAsync(tenantDatabaseId, cancellationToken);
    if (evidence is null)
    {
      // A database the registry does not hold cannot be authorised on the strength of absent evidence.
      return Result.Failure<TenantDatabaseRecoveryActivationDecision>(
        TenantStorageErrors.RecoveryActivationEvidenceUnavailable);
    }

    return Result.Success(TenantDatabaseRecoveryActivation.Decide(Inputs(evidence), clock.UtcNow));
  }

  // Mapping only. Every judgement lives in the domain, so an activation verdict cannot vary with the
  // orchestration that asked for it.
  private static TenantDatabaseRecoveryActivationInputs Inputs(
    TenantDatabaseRecoveryActivationEvidence evidence) =>
    new(
      new TenantDatabaseRecoveryReadinessInputs(
        evidence.HostingMode,
        evidence.PolicyExists,
        evidence.PolicyEnabled,
        evidence.ManagementMode,
        evidence.FullBackupIntervalMinutes,
        evidence.DifferentialBackupIntervalMinutes,
        evidence.TransactionLogBackupIntervalMinutes,
        evidence.RestoreVerificationIntervalDays,
        evidence.MaximumBackupAgeMinutes,
        evidence.LastSuccessfulFullBackupUtc,
        evidence.LastSuccessfulDifferentialBackupUtc,
        evidence.LastSuccessfulLogBackupUtc,
        evidence.LastRestoreVerificationUtc,

        // NEITHER OF THESE IS RECONSTRUCTED, and neither is read by the gate. The observed recovery model is
        // not persisted, and a chain break is already expressed by the held verdict being `Unprotected`.
        // The gate consumes the HELD status plus pure policy arithmetic, so deriving either here would add
        // a guess that nothing consumes.
        ObservedRecoveryModel: null,
        PlatformChainBreakDetected: false,
        HeldRecoveryReadinessStatus: evidence.RecoveryReadinessStatus),
      evidence.CurrentBaselineBackupRunId,
      evidence.VerifiedVerificationRunId,
      evidence.VerifiedSourceBackupRunId,
      evidence.VerifiedDepth,
      evidence.VerificationCompletedUtc);

  private static Error ErrorFor(TenantDatabaseRecoveryActivationDecision decision) => decision switch
  {
    TenantDatabaseRecoveryActivationDecision.RefusedUnprotected =>
      TenantStorageErrors.RecoveryActivationUnprotected,
    TenantDatabaseRecoveryActivationDecision.RefusedDegraded =>
      TenantStorageErrors.RecoveryActivationDegraded,
    TenantDatabaseRecoveryActivationDecision.RefusedRecoveryModelInvalid =>
      TenantStorageErrors.RecoveryActivationRecoveryModelInvalid,
    TenantDatabaseRecoveryActivationDecision.RefusedVerificationOverdue =>
      TenantStorageErrors.RecoveryActivationVerificationOverdue,
    TenantDatabaseRecoveryActivationDecision.RefusedBaselineUnavailable =>
      TenantStorageErrors.RecoveryActivationBaselineUnavailable,
    TenantDatabaseRecoveryActivationDecision.RefusedNeverRestoreVerified =>
      TenantStorageErrors.RecoveryActivationRestoreVerificationRequired,
    TenantDatabaseRecoveryActivationDecision.RefusedVerificationSupersededBaseline =>
      TenantStorageErrors.RecoveryActivationRestoreVerificationSuperseded,
    TenantDatabaseRecoveryActivationDecision.RefusedVerificationDepthInsufficient =>
      TenantStorageErrors.RecoveryActivationRestoreVerificationDepthInsufficient,
    TenantDatabaseRecoveryActivationDecision.RefusedVerificationStale =>
      TenantStorageErrors.RecoveryActivationRestoreVerificationStale,

    // RefusedRecoveryReadinessUnknown, and any decision a later slice adds without updating this map.
    // Failing closed on an unrecognised verdict is the only safe default for a gate.
    _ => TenantStorageErrors.RecoveryActivationReadinessUnknown
  };
}
