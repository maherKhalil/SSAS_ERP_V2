using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// The persistence boundary for D8's conservative reconciliation sweep. It is separate from the execution
// store so existing D7 workers cannot accidentally acquire a fleet scan or reconciliation transition.
public interface ITenantDatabaseRestoreVerificationReconciliationStore
{
  Task<IReadOnlyList<TenantDatabaseRestoreVerificationActiveRunRecord>> ListActiveAsync(
    long afterVerificationRunId,
    int take,
    CancellationToken cancellationToken = default);

  // Compare-and-set terminal transition. Every immutable identity fact read by the reconciler is repeated
  // here so a second reconciler or a resumed executor cannot be overwritten by a stale observation.
  Task<Result> ReconcileAbandonedAsync(
    TenantDatabaseRestoreVerificationReconciliationTransitionRequest request,
    CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseRestoreVerificationActiveRunRecord(
  long VerificationRunId,
  long TenantDatabaseId,
  long SourceBackupRunId,
  TenantDatabaseRestoreDepth Depth,
  string RestoreServerKey,
  string SourceServerKey,
  TenantDatabaseRestoreVerificationStatus Status,
  string? VerificationDatabaseName,
  DateTimeOffset StartedUtc);

public sealed record TenantDatabaseRestoreVerificationReconciliationTransitionRequest(
  TenantDatabaseRestoreVerificationActiveRunRecord Run,
  string? ReasonSummary,
  string Actor);
