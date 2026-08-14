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

  // Structurally impossible while UX_TenantDatabaseAssignments_ActiveTenant exists; retained so corruption
  // or a disabled constraint fails loudly instead of silently selecting one row.
  public static readonly Error AmbiguousActiveAssignment =
    new("TenantStorage.AmbiguousActiveAssignment", "The tenant has more than one active tenant database assignment.");

  public static readonly Error TenantDatabaseMissing =
    new("TenantStorage.TenantDatabaseMissing", "The assigned tenant database record could not be found.");

  public static readonly Error TenantDatabaseNotReady =
    new("TenantStorage.TenantDatabaseNotReady", "The assigned tenant database is not ready to serve traffic.");

  public static readonly Error UnsupportedHostingMode =
    new("TenantStorage.UnsupportedHostingMode", "The assigned tenant database uses a hosting mode that is not supported yet.");

  public static readonly Error ServerKeyNotConfigured =
    new("TenantStorage.ServerKeyNotConfigured", "The tenant database server key is not present in trusted configuration.");

  public static readonly Error DatabaseNameInvalid =
    new("TenantStorage.DatabaseNameInvalid", "The tenant database name is not valid for connection construction.");
}
