using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// A flat projection of the backup policy for the Application layer, so no EF entity escapes Infrastructure —
// the same rule TenantDatabaseAssignmentRecord and TenantDatabaseDescriptor follow.
//
// Carries the trusted DestinationKey and never a resolved destination: resolution to a physical location is
// an Infrastructure concern at execution time (ADR-022 §11), and nothing above Infrastructure has any use
// for the resolved value.
public sealed record TenantDatabaseBackupPolicyRecord(
  long TenantDatabaseBackupPolicyId,
  long TenantDatabaseId,
  bool Enabled,
  TenantDatabaseBackupManagementMode ManagementMode,
  string? DestinationKey,
  int? FullBackupIntervalMinutes,
  int? DifferentialBackupIntervalMinutes,
  int? TransactionLogBackupIntervalMinutes,
  int RetentionExpectationDays,
  int? RestoreVerificationIntervalDays,
  int? MaximumBackupAgeMinutes,
  TenantDatabaseBackupCompressionMode CompressionMode,
  TenantDatabaseBackupEncryptionMode EncryptionMode);
