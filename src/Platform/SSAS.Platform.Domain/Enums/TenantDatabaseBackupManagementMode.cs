namespace SSAS.Platform.Domain.Enums;

// WHO is permitted to execute backups of a physical tenant database (ADR-022 §5).
//
// Deliberately a SEPARATE concept from TenantDatabaseMigrationManagementMode, and deliberately not derived
// from HostingMode or StorageMode. The values look identical to migration's today, which is exactly what
// makes reuse tempting — but the authorities are independent: a customer may permit platform migrations
// while their DBA retains backup responsibility, or the reverse. ADR-018 already paid to correct this same
// conflation once when hosting vocabulary was reused for migration authority.
public enum TenantDatabaseBackupManagementMode
{
  // The platform schedules, executes, monitors and verifies backups. Default for PlatformManaged hosting —
  // shared or dedicated, because dedicated placement does not transfer durability ownership to the customer.
  AutomaticByPlatform = 1,

  // The platform may execute, but only under an explicit per-run approval. Absence of approval is DENIAL,
  // never default-allow.
  PlatformAfterApproval = 2,

  // The platform NEVER executes backups. It records the arrangement and, where a verification mechanism
  // exists, verifies evidence. Default for CustomerManaged hosting (ADR-021).
  CustomerDba = 3
}
