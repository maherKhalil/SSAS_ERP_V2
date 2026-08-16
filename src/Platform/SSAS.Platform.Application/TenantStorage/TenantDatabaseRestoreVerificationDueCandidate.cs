using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

public sealed record TenantDatabaseRestoreVerificationDueCandidate(
  long TenantDatabaseId,
  string SourceServerKey,
  TenantDatabaseHostingMode HostingMode,
  TenantDatabaseProvisioningStatus ProvisioningStatus,
  TenantDatabaseBackupManagementMode ManagementMode,
  bool PolicyEnabled,
  int? DifferentialBackupIntervalMinutes,
  int? TransactionLogBackupIntervalMinutes,
  int? RestoreVerificationIntervalDays,
  long? SourceBackupRunId,
  long? PreviousSuccessfulVerificationRunId,
  DateTimeOffset? PreviousSuccessfulVerificationCompletedUtc);
