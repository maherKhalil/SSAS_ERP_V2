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
}
