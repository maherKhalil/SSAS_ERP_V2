using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// One policy interpretation shared by D7's execution-time guard and D9's discovery projection. A durable
// run is valid only while the current policy still authorises exactly the depth it admitted.
public static class TenantDatabaseRestoreVerificationPolicy
{
  public static bool TryGetRequiredDepth(
    bool policyEnabled,
    TenantDatabaseBackupManagementMode managementMode,
    int? restoreVerificationIntervalDays,
    int? differentialBackupIntervalMinutes,
    int? transactionLogBackupIntervalMinutes,
    out TenantDatabaseRestoreDepth depth)
  {
    depth = default;
    if (!policyEnabled ||
      managementMode != TenantDatabaseBackupManagementMode.AutomaticByPlatform ||
      restoreVerificationIntervalDays is not > 0)
    {
      return false;
    }

    depth = transactionLogBackupIntervalMinutes is > 0
      ? TenantDatabaseRestoreDepth.FullWithDifferentialAndLog
      : differentialBackupIntervalMinutes is > 0
        ? TenantDatabaseRestoreDepth.FullWithDifferential
        : TenantDatabaseRestoreDepth.Full;
    return true;
  }

  public static bool TryGetRequiredDepth(
    TenantDatabaseBackupPolicyRecord? policy,
    out TenantDatabaseRestoreDepth depth)
  {
    depth = default;
    return policy is not null && TryGetRequiredDepth(
      policy.Enabled,
      policy.ManagementMode,
      policy.RestoreVerificationIntervalDays,
      policy.DifferentialBackupIntervalMinutes,
      policy.TransactionLogBackupIntervalMinutes,
      out depth);
  }
}
