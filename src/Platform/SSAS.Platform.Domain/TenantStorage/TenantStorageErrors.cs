using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.TenantStorage;

// Tenant-storage registry errors (ADR-017). Platform operational metadata, kept separate from both
// tenant-plane IdentityAccessErrors and platform-authority PlatformSupportErrors.
public static class TenantStorageErrors
{
  public static readonly Error ServerKeyRequired =
    new("TenantStorage.ServerKeyRequired", "A server key is required to register a tenant database.");

  public static readonly Error DatabaseNameRequired =
    new("TenantStorage.DatabaseNameRequired", "A database name is required to register a tenant database.");

  // CustomerManaged storage is dedicated to the authorized customer by definition; placing another
  // customer's tenant rows inside a customer-owned database is never an acceptable configuration.
  public static readonly Error CustomerManagedMustBeDedicated =
    new("TenantStorage.CustomerManagedMustBeDedicated", "A customer-managed tenant database must be dedicated.");

  public static readonly Error TenantRequired =
    new("TenantStorage.TenantRequired", "A valid tenant is required to assign a tenant database.");

  public static readonly Error TenantDatabaseRequired =
    new("TenantStorage.TenantDatabaseRequired", "A valid tenant database is required to create an assignment.");

  public static readonly Error RoutingVersionInvalid =
    new("TenantStorage.RoutingVersionInvalid", "A routing version must be greater than zero.");

  public static readonly Error AssignmentAlreadyEnded =
    new("TenantStorage.AssignmentAlreadyEnded", "The tenant database assignment has already been ended.");

  public static readonly Error AssignmentEndBeforeStart =
    new("TenantStorage.AssignmentEndBeforeStart", "A tenant database assignment cannot end before it was assigned.");

  // ---- Routing (TS-1C). Every one of these is a FAIL-CLOSED outcome: routing is refused rather than
  // satisfied from any other database (ADR-017 "No automatic fallback"). Messages never carry a connection
  // string, credential or raw SQL text.

  public static readonly Error TenantContextMissing =
    new("TenantStorage.TenantContextMissing", "A trusted tenant context is required to resolve tenant storage routing.");

  public static readonly Error ActiveAssignmentMissing =
    new("TenantStorage.ActiveAssignmentMissing", "The tenant has no active tenant database assignment.");

  // NOTE: there is deliberately no `AmbiguousActiveAssignment` or `TenantDatabaseMissing` error.
  // Both described conditions are structurally impossible — UX_TenantDatabaseAssignments_ActiveTenant
  // permits one active assignment per tenant, and the FK to TenantDatabase is Restrict — and neither was
  // reachable. Ambiguity surfaces instead as a hard failure from SingleOrDefaultAsync in the registry read,
  // which is the louder outcome; a declared error suggested a controlled Result path that did not exist.

  public static readonly Error TenantDatabaseNotReady =
    new("TenantStorage.TenantDatabaseNotReady", "The assigned tenant database is not ready to serve traffic.");

  public static readonly Error UnsupportedHostingMode =
    new("TenantStorage.UnsupportedHostingMode", "The assigned tenant database uses a hosting mode that is not supported yet.");

  public static readonly Error ServerKeyNotConfigured =
    new("TenantStorage.ServerKeyNotConfigured", "The tenant database server key is not present in trusted configuration.");

  // ---- Schema health and migration orchestration (ADR-018).

  public static readonly Error ConnectivityResultRequired =
    new("TenantStorage.ConnectivityResultRequired", "A completed connectivity check must record a definite result.");

  public static readonly Error MigrationIdentifierInvalid =
    new("TenantStorage.MigrationIdentifierInvalid", "The migration identifier is not valid.");

  public static readonly Error MigrationNotPermittedByManagementMode =
    new("TenantStorage.MigrationNotPermittedByManagementMode", "The tenant database's migration management mode does not permit the platform to apply migrations.");

  public static readonly Error MigrationAlreadyRunning =
    new("TenantStorage.MigrationAlreadyRunning", "A migration is already running for this tenant database.");

  public static readonly Error MigrationNotRunning =
    new("TenantStorage.MigrationNotRunning", "No migration is running for this tenant database.");

  public static readonly Error MigrationOwnershipNotAcquired =
    new("TenantStorage.MigrationOwnershipNotAcquired", "Migration ownership for this tenant database could not be acquired.");

  public static readonly Error MigrationOwnershipLost =
    new("TenantStorage.MigrationOwnershipLost", "Migration ownership for this tenant database was lost during the run.");

  public static readonly Error MigrationVerificationFailed =
    new("TenantStorage.MigrationVerificationFailed", "The tenant database's migration history did not reach the expected state after migrating.");

  // ---- Traffic gating (ADR-018). These deny ERP traffic; none of them falls back to another database.

  public static readonly Error TenantDatabaseUnavailable =
    new("TenantStorage.TenantDatabaseUnavailable", "The tenant database cannot currently be reached.");

  public static readonly Error DatabaseUpgradeRequired =
    new("TenantStorage.DatabaseUpgradeRequired", "The tenant database schema is not up to date for this application version.");

  public static readonly Error DatabaseUpgrading =
    new("TenantStorage.DatabaseUpgrading", "The tenant database is currently being migrated.");

  public static readonly Error SchemaHealthUnknown =
    new("TenantStorage.SchemaHealthUnknown", "The tenant database schema compatibility has not been verified.");

  public static readonly Error SchemaHealthStale =
    new("TenantStorage.SchemaHealthStale", "The tenant database schema compatibility check is too old to be trusted.");

  public static readonly Error DatabaseNameInvalid =
    new("TenantStorage.DatabaseNameInvalid", "The tenant database name is not valid for connection construction.");
}
