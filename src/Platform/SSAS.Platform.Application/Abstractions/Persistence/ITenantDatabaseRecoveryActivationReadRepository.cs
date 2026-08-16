using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// The READ-ONLY evidence boundary a Dedicated activation/cutover decision reads (TS-Storage Phase E).
//
// SEPARATE FROM THE BACKUP AND FLEET READ REPOSITORIES, because it answers a different question. Those
// project run history and fleet-scheduling candidates; this one assembles the exact facts an activation gate
// needs in a single consistent read, including the identity of the verification that succeeded — which
// neither of the others carries.
//
// KEYED ON THE PHYSICAL TenantDatabase, never on a tenant or an assignment. A shared database is one
// recovery target regardless of how many tenants it hosts, so activation evidence is a property of the
// physical database and querying it per tenant would return the same evidence repeatedly under different
// keys.
//
// STRICTLY READ-ONLY. Nothing here writes, and nothing here decides — the decision is the domain's
// (`TenantDatabaseRecoveryActivation`), so an activation verdict cannot vary by how a query was written.
public interface ITenantDatabaseRecoveryActivationReadRepository
{
  Task<TenantDatabaseRecoveryActivationEvidence?> FindActivationEvidenceAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);
}

// Everything an activation decision reads, from durable Platform evidence only.
//
// The verification block is the part that does not exist anywhere else: `LastRestoreVerificationUtc` alone
// answers "when", and the gate needs "which baseline, at what depth, and by which run" — a timestamp cannot
// distinguish a verification of the current chain from a verification of a full backup that has since been
// superseded (ADR-022 §17/§18).
public sealed record TenantDatabaseRecoveryActivationEvidence(
  long TenantDatabaseId,

  // ---- Registry identity. Activation is refused for anything the platform does not own the recovery of,
  // and the storage mode is carried so a caller can tell a Shared physical database from a Dedicated one.
  TenantDatabaseHostingMode HostingMode,
  TenantDatabaseStorageMode StorageMode,
  TenantDatabaseProvisioningStatus ProvisioningStatus,

  // ---- Active policy. `PolicyExists` is distinct from a disabled policy: no arrangement and a suspended
  // arrangement are different operator situations.
  bool PolicyExists,
  bool PolicyEnabled,
  TenantDatabaseBackupManagementMode ManagementMode,
  int? FullBackupIntervalMinutes,
  int? DifferentialBackupIntervalMinutes,
  int? TransactionLogBackupIntervalMinutes,
  int? RestoreVerificationIntervalDays,
  int? MaximumBackupAgeMinutes,

  // ---- The authoritative readiness verdict and the cached evidence behind it.
  TenantDatabaseRecoveryReadinessStatus RecoveryReadinessStatus,
  DateTimeOffset? LastSuccessfulFullBackupUtc,
  DateTimeOffset? LastSuccessfulDifferentialBackupUtc,
  DateTimeOffset? LastSuccessfulLogBackupUtc,
  DateTimeOffset? LastRestoreVerificationUtc,

  // ---- The chain a restore would start from right now.
  long? CurrentBaselineBackupRunId,

  // ---- The most recent SUCCEEDED restore verification, identified exactly. All four are null together
  // where no verification has ever succeeded.
  long? VerifiedVerificationRunId,
  long? VerifiedSourceBackupRunId,
  TenantDatabaseRestoreDepth? VerifiedDepth,
  DateTimeOffset? VerificationCompletedUtc);
