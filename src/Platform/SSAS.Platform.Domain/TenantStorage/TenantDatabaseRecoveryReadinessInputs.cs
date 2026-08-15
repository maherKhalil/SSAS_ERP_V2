using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// Every input the recovery-readiness verdict depends on (ADR-022 §6), gathered into one value so the
// evaluator can stay pure and the whole matrix can be tested without a database, a clock or a provider.
//
// POLICY AND EVIDENCE TOGETHER, deliberately. Readiness is the comparison of the two, and passing them
// separately would let a caller evaluate evidence against no policy — which is how "protection" quietly
// becomes "some backups exist".
public sealed record TenantDatabaseRecoveryReadinessInputs(
  TenantDatabaseHostingMode HostingMode,
  bool PolicyExists,
  bool PolicyEnabled,
  TenantDatabaseBackupManagementMode ManagementMode,
  int? FullBackupIntervalMinutes,
  int? DifferentialBackupIntervalMinutes,
  int? TransactionLogBackupIntervalMinutes,
  int? RestoreVerificationIntervalDays,
  int? MaximumBackupAgeMinutes,
  DateTimeOffset? LastSuccessfulFullBackupUtc,
  DateTimeOffset? LastSuccessfulDifferentialBackupUtc,
  DateTimeOffset? LastSuccessfulLogBackupUtc,
  DateTimeOffset? LastRestoreVerificationUtc,

  // OBSERVED, never assumed, and nullable because an evaluation that has not looked at the database does
  // not know it. A null model is not treated as valid: `RecoveryModelInvalid` is reported only on an
  // observed `Simple` under a log policy, so an unobserved model cannot manufacture a verdict either way.
  TenantDatabaseRecoveryModel? ObservedRecoveryModel = null,

  // Set when the platform-managed artifacts are known not to form the required restorable sequence — a
  // segment missing, superseded by an external non-copy-only backup, or expired. Outranks the depth
  // gradation (ADR-022 §17, v1.2).
  bool PlatformChainBreakDetected = false);
