using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// Owns the LIFECYCLE of one restore verification (ADR-022 §17, TS-Backup Phase D7).
//
// THE FIRST PRODUCTION CALLER OF THE RESTORE PROVIDER, and the boundary where eligibility is established.
// The provider restores what it is told to restore; deciding whether this database may be verified AT ALL,
// and what a completed restore MEANS, belongs here — the same division the backup executor and backup
// provider already keep.
//
// Invoked EXPLICITLY, one operation at a time. There is no scheduler behind it: fleet verification
// scheduling is a later slice, and until it exists nothing calls this autonomously.
public interface ITenantDatabaseRestoreVerificationExecutor
{
  // Executes the verification an admitted operation was created for.
  //
  // `expectedVerificationRunId` is not decoration. The executor requires THAT operation, not merely that
  // some operation exists, so a stale worker cannot execute another instance's admitted run.
  Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> ExecuteAsync(
    long tenantDatabaseId,
    long expectedVerificationRunId,
    TenantDatabaseRestoreDepth requestedDepth,
    CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseRestoreVerificationExecutionOutcome(
  long TenantDatabaseId,
  long VerificationRunId,
  TenantDatabaseRestoreVerificationStatus Status,

  // What the restore actually exercised. Null where nothing was restored.
  TenantDatabaseRestoreDepth? AchievedDepth,

  // TRUE ONLY WHEN EVERY CONDITION HELD: depth reached, database online, migration history readable at a
  // recognised position, and the schema probe succeeded. A restore that merely completed does not earn it.
  bool RestoreVerified,

  string? SafeErrorSummary);

// Read-only usability proof over the isolated restored physical database. The implementation owns the
// dedicated verification credential boundary; callers provide only trusted keys and the durable reserved
// database name.
public interface ITenantDatabaseRestoreVerificationProbe
{
  Task<TenantDatabaseRestoreProbeResult> ExecuteAsync(
    TenantDatabaseRestoreProbeRequest request,
    CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseRestoreProbeRequest(
  long TenantDatabaseId,
  long VerificationRunId,
  string RestoreServerKey,
  string SourceServerKey,
  string VerificationDatabaseName);

public sealed record TenantDatabaseRestoreProbeResult(
  TenantDatabaseRestoreProbeOutcome Outcome,
  TenantDatabaseRecoveryModel? ObservedRecoveryModel = null,
  string? AppliedMigration = null,
  string? SafeErrorSummary = null)
{
  public static TenantDatabaseRestoreProbeResult Succeeded(
    TenantDatabaseRecoveryModel recoveryModel,
    string appliedMigration) =>
    new(TenantDatabaseRestoreProbeOutcome.Succeeded, recoveryModel, appliedMigration);

  public static TenantDatabaseRestoreProbeResult Failed(string reason) =>
    new(TenantDatabaseRestoreProbeOutcome.Failed, SafeErrorSummary: reason);

  public static TenantDatabaseRestoreProbeResult Unavailable(string reason) =>
    new(TenantDatabaseRestoreProbeOutcome.Unavailable, SafeErrorSummary: reason);
}

public enum TenantDatabaseRestoreProbeOutcome
{
  Succeeded = 1,

  // The restored database is reachable but not demonstrably usable. Evidence about the backup.
  Failed = 2,

  // The probe could not reach or authenticate to the verification target. Not backup evidence.
  Unavailable = 3
}
