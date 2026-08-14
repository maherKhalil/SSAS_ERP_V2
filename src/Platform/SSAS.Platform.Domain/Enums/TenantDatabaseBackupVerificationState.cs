namespace SSAS.Platform.Domain.Enums;

// Verification state of one recorded backup set (ADR-022 §17). Two levels that are explicitly NOT
// substitutes for each other, which is why they are separate values rather than one boolean.
public enum TenantDatabaseBackupVerificationState
{
  // Nothing has looked at this backup set yet.
  NotVerified = 1,

  // RESTORE VERIFYONLY passed: the set is readable and its checksums are valid. This is NOT proof of
  // recoverability — it restores nothing and never looks at what a restored database would contain.
  ReadabilityVerified = 2,

  // An actual restore to a disposable database completed and the restored database was probed. The only
  // evidence that answers the real question.
  RestoreVerified = 3,

  VerificationFailed = 4
}
