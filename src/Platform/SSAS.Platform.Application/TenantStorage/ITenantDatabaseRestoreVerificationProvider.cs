using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Actual isolated restore verification (ADR-022 §17, TS-Backup Phase D6).
//
// The provider RESTORES and REPORTS; it never mutates Platform domain state, exactly as the backup provider
// never does. The executor decides what an outcome means.
//
// WHAT THIS SLICE DOES NOT DO: it does not drop the verification database it creates. Cleanup execution
// waits on the destructive-permission proof, which this environment cannot yet produce (see D6 notes), and
// shipping a `DROP DATABASE` path against an unproven permission model would be the wrong order to do those
// two things in.
public interface ITenantDatabaseRestoreVerificationProvider
{
  Task<TenantDatabaseRestoreVerificationResult> ExecuteAsync(
    TenantDatabaseRestoreVerificationRequest request,
    CancellationToken cancellationToken = default);
}

// Everything the provider needs, and nothing it could be misled by.
//
// The chain arrives ALREADY SELECTED and fixed, so the provider restores the sequence the operation was
// admitted for rather than re-deciding at execution time — which is what keeps the recovery point stable.
public sealed record TenantDatabaseRestoreVerificationRequest(
  long TenantDatabaseId,
  long VerificationRunId,
  TenantDatabaseRestoreChain Chain,
  string RestoreServerKey,
  string SourceServerKey,
  string VerificationDatabaseName);

// What the provider observed. Distinct from the persisted run, so a provider concern never becomes domain
// state by accident.
//
// `AchievedDepth` travels with the outcome because a successful restore is not self-describing: the same
// `RestoredAndOnline` can represent a full-only sequence or a full-differential-log one, and D7 must be able
// to refuse `RestoreVerified` at a depth the sequence never reached. Null where nothing was restored.
public sealed record TenantDatabaseRestoreVerificationResult(
  TenantDatabaseRestoreVerificationOutcome Outcome,
  string? VerificationDatabaseName = null,
  int RestoredStepCount = 0,
  TenantDatabaseRestoreDepth? AchievedDepth = null,
  DateTimeOffset? StartedUtc = null,
  DateTimeOffset? CompletedUtc = null,
  string? SafeErrorSummary = null);

public enum TenantDatabaseRestoreVerificationOutcome
{
  // The whole selected sequence restored and the database came online.
  //
  // NOT the same as RestoreVerified. The application and schema probes that ADR-022 §17 also requires belong
  // to D7, so this outcome deliberately stops short of claiming the database is USABLE — only that it
  // restored and is online. Recording it as full verification here would fake the probe that has not run.
  RestoredAndOnline = 1,

  // The restore itself did not complete.
  RestoreFailed = 2,

  // The sequence completed but the database is not online, so nothing was demonstrated.
  NotOnline = 3,

  // Refused before touching anything: the target already exists, the chain is unusable, or the admitted
  // operation drifted. A controlled non-execution, not a recovery failure.
  BlockedByPrecondition = 4,

  // Could not begin or complete for reasons independent of the artifacts — verification host unavailable,
  // configuration unresolvable. NOT evidence about the backup (ADR-022 §17, v1.2).
  InfrastructureUnavailable = 5
}
