namespace SSAS.Platform.Domain.Enums;

// WHO is permitted to apply DDL to a physical tenant database (ADR-018).
//
// Deliberately a separate concept from HostingMode, with a deliberately distinct vocabulary. ADR-018
// records why: an earlier draft reused PlatformManaged/CustomerManaged for both, which made the perfectly
// ordinary configuration "customer-hosted database we are permitted to migrate" read as a contradiction in
// configuration, logs and operator tooling.
public enum TenantDatabaseMigrationManagementMode
{
  // The orchestrator may migrate this database as part of a release. Typical for PlatformManaged hosting.
  AutomaticByPlatform = 1,

  // The orchestrator may migrate, but only after an explicit per-run approval. Absence of approval is
  // treated as DENIAL, never as default-allow.
  PlatformAfterApproval = 2,

  // The platform NEVER applies DDL. It detects and reports pending migrations; a customer DBA executes
  // them, and the platform verifies the result against observed history rather than an assertion.
  CustomerDba = 3
}
