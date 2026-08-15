using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Executes one managed backup of ONE physical tenant database, end to end (ADR-022, TS-Backup Phase B).
//
// This is the write-oriented counterpart to the backup read repository, and the single entry point through
// which a backup can happen at all. It is invoked EXPLICITLY — by deployment tooling or an operator action —
// and never from the request path. There is no scheduler, no timer and no hosted service behind it: fleet
// scheduling is Phase C and is blocked until the Phase B session-loss question is settled (ADR-022 §14).
//
// The executor owns the RUN LIFECYCLE — authority checks, run creation, status transitions, and the recovery
// observation that follows a proven backup. The provider owns only the SQL Server operation and its
// evidence. That split follows the ADR-018 precedent, where the migration orchestrator owns lifecycle and
// the ownership primitive stays separate.
public interface ITenantDatabaseBackupExecutor
{
  // Returns the recorded outcome. A refusal — wrong authority, ownership held, an operation already in
  // flight — is a SUCCESSFUL call reporting a controlled non-execution, not an error.
  Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    CancellationToken cancellationToken = default);

  // CONDITIONAL execution, for scheduler-originated work only (TS-Backup Phase C).
  //
  // `dueAnchorUtc` is the last-successful timestamp the scheduling decision was based on. If a
  // platform-managed backup of this operation has completed since then — because another instance got there
  // first — the decision is stale and this call reports a controlled skip instead of taking a second backup.
  //
  // Deliberately a SEPARATE method rather than an optional argument on the one above. Manual execution means
  // "take this backup now" and must never become conditional on a schedule; keeping the two intents apart in
  // the contract makes that impossible to blur by passing a default.
  Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteScheduledAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    DateTimeOffset? dueAnchorUtc,
    CancellationToken cancellationToken = default);
}

// The recorded result of one execution attempt, projected for the caller. Carries the run identifier so an
// operator can find the persisted history, and never carries a resolved destination.
public sealed record TenantDatabaseBackupExecutionOutcome(
  long TenantDatabaseId,
  long BackupRunId,
  string OperationProviderKey,
  string OperationCode,
  Domain.Enums.TenantDatabaseBackupRunStatus Status,
  string? ProviderBackupSetIdentity,
  string? SafeErrorSummary);
