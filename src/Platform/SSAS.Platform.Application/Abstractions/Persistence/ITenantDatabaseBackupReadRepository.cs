using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Narrow read access to backup policy and run history (ADR-022).
//
// READ ONLY, deliberately. There is no ExecuteBackupAsync, RunFullBackupAsync or anything else that could
// cause a backup: Phase A establishes what backup state exists and how it is persisted, and says nothing
// about how SQL Server performs a backup. Execution arrives with the provider in Phase B, and until it does
// no contract in this assembly grants backup authority to anything.
public interface ITenantDatabaseBackupReadRepository
{
  // The single policy for one PHYSICAL database, or null when none is configured. Null is a meaningful
  // answer — an unconfigured database is unprotected, not an error.
  Task<TenantDatabaseBackupPolicyRecord?> FindPolicyAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);

  // Recent run history for one physical database, most recent first. Bounded by `take` because history
  // accumulates fastest of anything in this model.
  Task<IReadOnlyList<TenantDatabaseBackupRunRecord>> ListRecentRunsAsync(
    long tenantDatabaseId,
    int take,
    CancellationToken cancellationToken = default);

  // The latest SUCCESSFUL run of one provider operation — the evidence a future readiness evaluation reads
  // to decide whether a chain has a baseline and how old it is. Operation is provider-scoped, so callers
  // pass SQL Server's codes rather than a universal enum.
  Task<TenantDatabaseBackupRunRecord?> FindLatestSuccessfulRunAsync(
    long tenantDatabaseId,
    string operationProviderKey,
    string operationCode,
    CancellationToken cancellationToken = default);

  // The cached recovery observations from the physical database row (ADR-022 §6, TS-Backup Phase D).
  //
  // Needed because readiness is a comparison of POLICY against EVIDENCE, and the verification half of that
  // evidence lives on `TenantDatabase` rather than in run history. Reading it is what stops a database with
  // a recent successful verification being reported `VerificationOverdue` after every backup.
  //
  // Null when the physical database does not exist.
  Task<TenantDatabaseRecoveryEvidenceRecord?> FindRecoveryEvidenceAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);
}

// The recovery observations cached on the physical database row. Timestamps only: the STATUS is a
// conclusion drawn from these against policy, never a stored input to that conclusion.
public sealed record TenantDatabaseRecoveryEvidenceRecord(
  long TenantDatabaseId,
  DateTimeOffset? LastSuccessfulFullBackupUtc,
  DateTimeOffset? LastSuccessfulDifferentialBackupUtc,
  DateTimeOffset? LastSuccessfulLogBackupUtc,
  DateTimeOffset? LastRestoreVerificationUtc);
