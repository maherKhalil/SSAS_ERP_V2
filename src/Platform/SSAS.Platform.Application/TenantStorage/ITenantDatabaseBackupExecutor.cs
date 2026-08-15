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
