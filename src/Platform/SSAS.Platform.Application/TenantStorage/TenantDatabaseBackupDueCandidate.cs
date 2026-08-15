using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// One physical database the fleet scheduler may consider for a backup (ADR-022 §13, TS-Backup Phase C).
//
// A projection, deliberately narrow. It carries the identity, the routing bucket and the timing facts needed
// to decide WHETHER something is due — and nothing that could execute anything. No connection string, no
// resolved destination, no credential, no tenant assignment list. The scheduler's whole job is to name a
// database and an operation; the executor and provider resolve everything else authoritatively at execution.
//
// The due-time inputs are the LastSuccessful*BackupUtc fields from `TenantDatabase`, which the Phase B
// executor maintains after every proven backup. That is what lets a fleet sweep answer "what is due?" from a
// single paged query instead of a run-history lookup per database.
public sealed record TenantDatabaseBackupDueCandidate(
  long TenantDatabaseId,
  string ServerKey,
  TenantDatabaseHostingMode HostingMode,
  TenantDatabaseProvisioningStatus ProvisioningStatus,
  TenantDatabaseBackupManagementMode ManagementMode,
  bool PolicyEnabled,
  int? FullBackupIntervalMinutes,
  int? DifferentialBackupIntervalMinutes,
  int? TransactionLogBackupIntervalMinutes,
  DateTimeOffset? LastSuccessfulFullBackupUtc,
  DateTimeOffset? LastSuccessfulDifferentialBackupUtc,
  DateTimeOffset? LastSuccessfulLogBackupUtc);
