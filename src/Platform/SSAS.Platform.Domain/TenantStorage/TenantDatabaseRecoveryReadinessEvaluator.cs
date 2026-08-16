using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// Turns backup and verification evidence into a recovery-readiness verdict (ADR-022 §6, v1.2).
//
// DELIBERATELY IN THE DOMAIN, not in the SQL Server provider. The provider's job is to produce evidence;
// deciding what evidence MEANS is a durability policy question that must not vary by provider — otherwise
// "protected" would mean whatever each provider's implementation happened to check.
//
// PURE. Every input is passed in, `nowUtc` included, so the whole matrix is unit-testable without a clock,
// a database or a provider.
public static class TenantDatabaseRecoveryReadinessEvaluator
{
  // The full evaluation (ADR-022 §6).
  //
  // `Protected` MEANS EVERY RECOVERY OBLIGATION THE ACTIVE POLICY REQUIRES IS CURRENTLY SATISFIED. It does
  // NOT, by itself, mean the database has undergone an actual restore verification: where the policy sets a
  // verification interval that verification is one of the obligations and `Protected` implies it, and where
  // the policy sets none it is not an obligation and `Protected` is reached without it. Restore-provenness
  // stays separately observable through `LastRestoreVerificationUtc` and verification history — an operator
  // asking "has this ever actually been restored?" reads that, not this status.
  public static TenantDatabaseRecoveryReadinessStatus Evaluate(
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    // CustomerManaged is never concluded from here. The platform owns neither the server nor the data's
    // disposition, has no supported connectivity path to one, and must never report `Protected` on the
    // strength of a customer's assertion (ADR-022 §12, compliance rule 7).
    if (inputs.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return TenantDatabaseRecoveryReadinessStatus.Unknown;
    }

    // No policy, or one the platform may not execute, is not a database that is merely un-evaluated — it is
    // a platform-managed database with no arranged protection.
    if (!inputs.PolicyExists || !inputs.PolicyEnabled ||
      inputs.ManagementMode != TenantDatabaseBackupManagementMode.AutomaticByPlatform)
    {
      return TenantDatabaseRecoveryReadinessStatus.Unprotected;
    }

    // A KNOWN REQUIRED-CHAIN BREAK OUTRANKS THE DEPTH GRADATION BELOW (ADR-022 §17, v1.2). Where the
    // platform-managed artifacts cannot reconstruct the required recovery path at all — a segment missing,
    // superseded, or expired out from under the retention expectation — there is no usable required
    // recovery point, and calling that `Degraded` would soften an established break.
    if (inputs.PlatformChainBreakDetected)
    {
      return TenantDatabaseRecoveryReadinessStatus.Unprotected;
    }

    // No baseline is the definition of no usable recovery position.
    if (inputs.LastSuccessfulFullBackupUtc is null)
    {
      return TenantDatabaseRecoveryReadinessStatus.Unprotected;
    }

    // Recovery model is checked AFTER the baseline, so a database with no backups at all reports the more
    // fundamental problem rather than a model complaint.
    if (RequiresLogChain(inputs) && inputs.ObservedRecoveryModel == TenantDatabaseRecoveryModel.Simple)
    {
      return TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid;
    }

    // Verification freshness is evaluated before backup ages because an unverified chain is a different and
    // more specific statement than a late backup, and it is the one ADR-022 §18 gates cutover on.
    if (IsVerificationOverdue(inputs, nowUtc))
    {
      return TenantDatabaseRecoveryReadinessStatus.VerificationOverdue;
    }

    // A log-protection claim cannot become Protected without an observed recovery model. In particular, an
    // infrastructure-only verification outcome has learned nothing new about that model and must not erase
    // a held non-Protected verdict merely because the input is absent. `Degraded` is the conservative D1
    // result until a later successful probe supplies Full, BulkLogged, or Simple.
    if (RequiresLogChain(inputs) && inputs.ObservedRecoveryModel is null)
    {
      return TenantDatabaseRecoveryReadinessStatus.Degraded;
    }

    if (IsAnyScheduledBackupOverdue(inputs, nowUtc) || IsRecoveryPointStale(inputs, nowUtc))
    {
      return TenantDatabaseRecoveryReadinessStatus.Degraded;
    }

    return TenantDatabaseRecoveryReadinessStatus.Protected;
  }

  // `VerificationOverdue` applies when, and ONLY when, the policy requires periodic restore verification AND
  // no successful verification exists or the last one has aged past the interval (ADR-022 §6, v1.2).
  //
  // A NULL INTERVAL MEANS NO PERIODIC OBLIGATION — not a zero interval, not immediately overdue, not
  // unknown. Reporting a database overdue for work its policy never asked for would leave the fleet's
  // most heavily exercised shared chains permanently amber, which erodes the signal exactly where it needs
  // to be readable.
  public static bool IsVerificationOverdue(
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    if (inputs.RestoreVerificationIntervalDays is not { } days || days <= 0)
    {
      return false;
    }

    if (inputs.LastRestoreVerificationUtc is not { } lastVerified)
    {
      // Required, and never performed.
      return true;
    }

    return nowUtc > lastVerified.AddDays(days);
  }

  // What a verification FAILURE means for readiness (ADR-022 §17, v1.2).
  //
  // Returns `null` where readiness must not change — which is not the same as "no opinion". A cleanup
  // failure and an infrastructure outage are both cases where the evidence the platform holds is still the
  // best evidence available, and overwriting it would replace a fact with a guess.
  public static TenantDatabaseRecoveryReadinessStatus? EvaluateAfterVerificationFailure(
    TenantDatabaseVerificationFailure failure,
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    return failure switch
    {
      // The minimum recoverable position cannot be demonstrated at all.
      TenantDatabaseVerificationFailure.BaselineRestoreFailed =>
        TenantDatabaseRecoveryReadinessStatus.Unprotected,

      // A restorable baseline whose deeper required segment failed to apply or validate. Recoverability
      // exists; the deeper capability the policy claims is not currently proven.
      TenantDatabaseVerificationFailure.DeeperChainRestoreFailed =>
        TenantDatabaseRecoveryReadinessStatus.Degraded,

      // The required path cannot be reconstructed from platform-owned artifacts at all.
      TenantDatabaseVerificationFailure.RequiredChainBreak =>
        TenantDatabaseRecoveryReadinessStatus.Unprotected,

      // Restored and online, but not demonstrably usable as the tenant database the platform would need to
      // recover. A database that restores to something unusable is not a recovery position.
      TenantDatabaseVerificationFailure.PostRestoreProbeFailed =>
        TenantDatabaseRecoveryReadinessStatus.Unprotected,

      // CLEANUP ONLY. Recoverability was proven before this failed; a failed drop is an operational and
      // orphan condition, not evidence about the chain.
      TenantDatabaseVerificationFailure.CleanupFailed => null,

      // INFRASTRUCTURE FAILURE IS NOT EVIDENCE ABOUT THE BACKUP. The attempt could not begin or complete for
      // reasons independent of the artifacts, so existing evidence is neither upgraded nor invalidated:
      // readiness is recomputed from what is already held, which will report `VerificationOverdue` if
      // verification is required and has now aged out. Converting a verification-host outage into
      // `Unprotected` would report a well-protected database as unrecoverable on an unrelated failure.
      TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable =>
        EvaluateDuringUncertainty(inputs, nowUtc),

      _ => null
    };
  }

  private static TenantDatabaseRecoveryReadinessStatus EvaluateDuringUncertainty(
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc)
  {
    var current = Evaluate(inputs, nowUtc);

    // The aggregate row cannot yet retain why a deeper verification established `Degraded`. Until new
    // recovery evidence resolves that fact, infrastructure or metadata uncertainty must not turn fresh
    // timestamps into a false `Protected`. Any adverse verdict established by current evidence still wins.
    return current == TenantDatabaseRecoveryReadinessStatus.Protected &&
      inputs.HeldRecoveryReadinessStatus == TenantDatabaseRecoveryReadinessStatus.Degraded
        ? TenantDatabaseRecoveryReadinessStatus.Degraded
        : current;
  }

  // The PARTIAL observation available immediately after a successful backup.
  //
  // Deliberately CANNOT reach `Protected`, and the reason is honest rather than conservative: this path has
  // not observed the database's recovery model or its chain continuity, and `Protected` asserts both. The
  // full evaluation above belongs to the verification/readiness sweep, which observes them.
  //
  // What it DOES fix relative to Phase B is the mislabelling: a database whose policy requires no periodic
  // restore verification is no longer reported `VerificationOverdue` for work it never asked for.
  public static TenantDatabaseRecoveryReadinessStatus EvaluateAfterSuccessfulBackup(
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    return IsVerificationOverdue(inputs, nowUtc)
      ? TenantDatabaseRecoveryReadinessStatus.VerificationOverdue
      : TenantDatabaseRecoveryReadinessStatus.Degraded;
  }

  // The depth a verification must exercise, derived from what the policy claims (ADR-022 §17, v1.2).
  public static TenantDatabaseRestoreDepth RequiredDepth(TenantDatabaseRecoveryReadinessInputs inputs)
  {
    ArgumentNullException.ThrowIfNull(inputs);

    if (RequiresLogChain(inputs))
    {
      return TenantDatabaseRestoreDepth.FullWithDifferentialAndLog;
    }

    return IsScheduled(inputs.DifferentialBackupIntervalMinutes)
      ? TenantDatabaseRestoreDepth.FullWithDifferential
      : TenantDatabaseRestoreDepth.Full;
  }

  private static bool RequiresLogChain(TenantDatabaseRecoveryReadinessInputs inputs) =>
    IsScheduled(inputs.TransactionLogBackupIntervalMinutes);

  private static bool IsScheduled(int? intervalMinutes) => intervalMinutes is > 0;

  private static bool IsAnyScheduledBackupOverdue(
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc) =>
    IsOverdue(inputs.FullBackupIntervalMinutes, inputs.LastSuccessfulFullBackupUtc, nowUtc) ||
    IsOverdue(inputs.DifferentialBackupIntervalMinutes, inputs.LastSuccessfulDifferentialBackupUtc, nowUtc) ||
    IsOverdue(inputs.TransactionLogBackupIntervalMinutes, inputs.LastSuccessfulLogBackupUtc, nowUtc);

  private static bool IsOverdue(int? intervalMinutes, DateTimeOffset? lastSuccessUtc, DateTimeOffset nowUtc)
  {
    if (!IsScheduled(intervalMinutes))
    {
      return false;
    }

    // Scheduled but never taken. A differential or log type that has never run has no position at all.
    return lastSuccessUtc is not { } last || nowUtc > last.AddMinutes(intervalMinutes!.Value);
  }

  // The operative V1 recovery-point check (ADR-022 §4). Measured against the most recent recovery point of
  // any type the policy schedules, because that is what a restore would actually recover to.
  private static bool IsRecoveryPointStale(
    TenantDatabaseRecoveryReadinessInputs inputs,
    DateTimeOffset nowUtc)
  {
    if (inputs.MaximumBackupAgeMinutes is not { } maximumAge || maximumAge <= 0)
    {
      return false;
    }

    var latest = inputs.LastSuccessfulFullBackupUtc;
    latest = Later(latest, inputs.LastSuccessfulDifferentialBackupUtc);
    latest = Later(latest, inputs.LastSuccessfulLogBackupUtc);

    return latest is not { } recoveryPoint || nowUtc > recoveryPoint.AddMinutes(maximumAge);
  }

  private static DateTimeOffset? Later(DateTimeOffset? left, DateTimeOffset? right)
  {
    if (left is null)
    {
      return right;
    }

    return right is null || right < left ? left : right;
  }
}
