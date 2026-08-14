namespace SSAS.Platform.Domain.Enums;

// Is a physical tenant database's schema compatible with THIS application version (ADR-018)?
//
// The authority is the database's own `tenant.__EFMigrationsHistory` compared against the deployed Tenant
// migration catalog. Platform-side fields caching this conclusion are never the sole basis for a
// correctness decision — a metadata column claiming a version the database does not actually have is the
// classic failure this model exists to prevent.
public enum TenantDatabaseSchemaCompatibilityStatus
{
  // Pre-verification, or a check that could not complete. DENIES traffic.
  Unknown = 1,

  // Applied history exactly matches the deployed catalog.
  UpToDate = 2,

  // The database is behind: every applied migration is known and in order, and migrations remain to apply.
  // Upgrade-required, and traffic is denied until it is applied — by the orchestrator or a customer DBA.
  PendingMigrations = 3,

  // The database has migrations this application does not know. An older instance must never blindly serve
  // a newer database, and downgrading is never attempted automatically.
  AheadOfApplication = 4,

  // The lineage diverges — an unknown migration interleaved with known ones, or an unexpected order.
  // Migrations must not be appended blindly to an unrecognised history; this requires human investigation.
  MigrationHistoryMismatch = 5
}
