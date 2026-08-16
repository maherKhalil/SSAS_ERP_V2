using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// May a Dedicated tenant database be ACTIVATED — first activation, or the cutover that flips routing onto it
// (ADR-017 cutover ordering, ADR-022 §18)?
//
// TWO INDEPENDENT REQUIREMENTS, and the whole point of this type is that neither implies the other:
//
//   1. `RecoveryReadinessStatus` is `Protected` — every recovery obligation the ACTIVE POLICY requires is
//      currently satisfied.
//   2. An ACTUAL restore verification succeeded against the recovery path that is required RIGHT NOW.
//
// PROTECTED ALONE IS NOT ENOUGH, and this is not a belt-and-braces precaution — it is a direct consequence
// of what Phase D decided `Protected` means. Where a policy sets no verification interval, verification is
// not one of its obligations, so `Protected` is reached without any database ever having been restored
// (ADR-022 §6). That is the correct meaning for a fleet health signal and the wrong bar for moving a
// tenant's live traffic onto a database whose recoverability has never been demonstrated. This type adds the
// second requirement rather than changing the first, because redefining `Protected` would silently re-grade
// the entire fleet.
//
// EXACT EVIDENCE, NEVER "HAS THIS EVER BEEN RESTORE-TESTED". The question is whether the verification that
// succeeded exercised the chain the platform would actually restore today: the same baseline, and at least
// the depth the current policy claims. A verification of a superseded full is a true statement about a
// recovery path that no longer exists.
//
// PURE. Every input is passed in, `nowUtc` included.
public static class TenantDatabaseRecoveryActivation
{
  public static TenantDatabaseRecoveryActivationDecision Decide(
    TenantDatabaseRecoveryActivationInputs inputs,
    DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    // ---- HOSTING MODE, CHECKED DIRECTLY AND FIRST.
    //
    // The platform owns neither the server nor the recovery of a CustomerManaged database and must never
    // activate one on the strength of an asserted recovery position (ADR-022 §12, compliance rule 7).
    //
    // The readiness evaluator already refuses CustomerManaged structurally, so this was previously implied
    // — but only TRANSITIVELY, through a held status that this gate reads rather than recomputes. That
    // relied on `HostingMode` being immutable after registration, which is true today and is not a property
    // this gate should depend on: the day a hosting-mode transition is added, a database that had been
    // `Protected` while PlatformManaged would carry that status across and pass. Asserting it here costs one
    // comparison and removes the coupling.
    if (inputs.Readiness.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedRecoveryReadinessUnknown;
    }

    // ---- REQUIREMENT 1: the authoritative readiness verdict.
    //
    // The HELD status is used rather than recomputed here, and that is deliberate. A recomputation cannot
    // reconstruct `ObservedRecoveryModel` — it is an observation made by the post-restore probe and is not
    // persisted — so re-deriving readiness would report every log-chain database `Degraded` and make Level C
    // databases permanently unactivatable. The held value is written by the recovery-readiness writer from
    // evidence that included that observation, and it is the value ADR-022 §6 defines as authoritative.
    if (inputs.Readiness.HeldRecoveryReadinessStatus != TenantDatabaseRecoveryReadinessStatus.Protected)
    {
      return Refuse(inputs.Readiness.HeldRecoveryReadinessStatus);
    }

    // ...but a held verdict is a projection, and projections age. This is the one part of the evaluation that
    // is pure arithmetic over persisted facts, so it can be rechecked against the current clock without the
    // missing observation — which catches a `Protected` that was true when written and is not true now.
    if (TenantDatabaseRecoveryReadinessEvaluator.IsVerificationOverdue(inputs.Readiness, nowUtc))
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedVerificationOverdue;
    }

    // ---- REQUIREMENT 2: the exact restore verification.

    // A `Protected` database with no current baseline is an inconsistency between the projection and the run
    // history. Refuse rather than reconcile: activation is not the place to resolve disagreeing evidence.
    if (inputs.CurrentBaselineBackupRunId is not { } currentBaseline || currentBaseline <= 0)
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedBaselineUnavailable;
    }

    // Every part of the verification identity is required together. A run id without a source backup, a
    // depth or a completion time is a partial record, and a partial record is not evidence.
    if (inputs.VerifiedVerificationRunId is not { } verificationRunId || verificationRunId <= 0 ||
      inputs.VerifiedSourceBackupRunId is not { } verifiedBaseline ||
      inputs.VerifiedDepth is not { } verifiedDepth ||
      inputs.VerificationCompletedUtc is not { } verifiedUtc)
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedNeverRestoreVerified;
    }

    // THE SUPERSEDED-BASELINE REFUSAL. The verification proved a chain rooted in a full backup that is no
    // longer the one a restore would start from, so it says nothing about the current recovery path.
    if (verifiedBaseline != currentBaseline)
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedVerificationSupersededBaseline;
    }

    // The verification must have exercised at least what the policy currently claims. A Full-only
    // verification does not license activating a database whose policy promises log-level recovery.
    if (verifiedDepth < TenantDatabaseRecoveryReadinessEvaluator.RequiredDepth(inputs.Readiness))
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedVerificationDepthInsufficient;
    }

    // Freshness measured against the EXACT RUN's completion, not the aggregate timestamp. The two normally
    // agree; where they do not, the aggregate is a cached projection and the run is the record.
    if (inputs.Readiness.RestoreVerificationIntervalDays is { } days && days > 0 &&
      nowUtc > verifiedUtc.AddDays(days))
    {
      return TenantDatabaseRecoveryActivationDecision.RefusedVerificationStale;
    }

    return TenantDatabaseRecoveryActivationDecision.Allowed;
  }

  private static TenantDatabaseRecoveryActivationDecision Refuse(
    TenantDatabaseRecoveryReadinessStatus status) => status switch
    {
      TenantDatabaseRecoveryReadinessStatus.Unprotected =>
        TenantDatabaseRecoveryActivationDecision.RefusedUnprotected,
      TenantDatabaseRecoveryReadinessStatus.Degraded =>
        TenantDatabaseRecoveryActivationDecision.RefusedDegraded,
      TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid =>
        TenantDatabaseRecoveryActivationDecision.RefusedRecoveryModelInvalid,
      TenantDatabaseRecoveryReadinessStatus.VerificationOverdue =>
        TenantDatabaseRecoveryActivationDecision.RefusedVerificationOverdue,

      // Unknown, and anything a later status value might add. An unrecognised readiness verdict is not a
      // reason to proceed.
      _ => TenantDatabaseRecoveryActivationDecision.RefusedRecoveryReadinessUnknown
    };
}

// The facts an activation decision requires.
//
// Composed from the readiness inputs rather than repeating them, so the two evaluations can never disagree
// about the policy they are reading, and the exact verification identity is carried alongside.
public sealed record TenantDatabaseRecoveryActivationInputs(
  TenantDatabaseRecoveryReadinessInputs Readiness,

  // The full backup a restore would start from RIGHT NOW.
  long? CurrentBaselineBackupRunId,

  // The most recent SUCCEEDED restore verification, identified exactly. Null where none exists.
  long? VerifiedVerificationRunId,
  long? VerifiedSourceBackupRunId,
  TenantDatabaseRestoreDepth? VerifiedDepth,
  DateTimeOffset? VerificationCompletedUtc);

// Why activation was allowed or refused. Granular because each refusal maps to a DIFFERENT operator action:
// a superseded baseline is resolved by running a verification, `Unprotected` by fixing the backups, and
// `RecoveryModelInvalid` by an ALTER the platform will never issue itself.
public enum TenantDatabaseRecoveryActivationDecision
{
  Allowed = 1,

  // ---- Readiness refusals.
  RefusedRecoveryReadinessUnknown = 2,
  RefusedUnprotected = 3,
  RefusedDegraded = 4,
  RefusedRecoveryModelInvalid = 5,
  RefusedVerificationOverdue = 6,

  // ---- Exact-evidence refusals.
  RefusedBaselineUnavailable = 7,
  RefusedNeverRestoreVerified = 8,
  RefusedVerificationSupersededBaseline = 9,
  RefusedVerificationDepthInsufficient = 10,
  RefusedVerificationStale = 11
}
