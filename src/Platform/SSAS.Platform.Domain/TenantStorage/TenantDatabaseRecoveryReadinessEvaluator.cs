using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// Turns backup evidence into a recovery-readiness verdict (ADR-022 §6).
//
// DELIBERATELY IN THE DOMAIN, not in the SQL Server provider. The provider's job is to produce evidence;
// deciding what evidence MEANS is a durability policy question that must not vary by provider — otherwise
// "protected" would mean whatever each provider's implementation happened to check.
//
// Phase B scope is narrow on purpose: it answers only "what does a successful backup make this database?"
// The full evaluation — recovery model validity, log-chain completeness, backup age against policy,
// verification freshness — arrives with the phases that own that evidence.
public static class TenantDatabaseRecoveryReadinessEvaluator
{
  // A SUCCESSFUL BACKUP NEVER MAKES A DATABASE `Protected`.
  //
  // ADR-022 §6 requires restore verification not to be overdue, and the first ACTUAL restore verification is
  // Phase D. A database that has just taken a good backup has a baseline that has never been restored even
  // once — which is precisely the position `VerificationOverdue` exists to describe, and precisely the
  // position ADR-022 §18 refuses to cut over on.
  //
  // Reporting `Protected` here would be the single most damaging shortcut available in this slice: every
  // downstream gate reads that value, and it would be claiming demonstrated recoverability on the strength
  // of an unexercised chain.
  public static TenantDatabaseRecoveryReadinessStatus EvaluateAfterSuccessfulBackup() =>
    TenantDatabaseRecoveryReadinessStatus.VerificationOverdue;
}
