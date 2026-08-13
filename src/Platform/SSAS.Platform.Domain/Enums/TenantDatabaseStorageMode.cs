namespace SSAS.Platform.Domain.Enums;

// How many tenants share one physical tenant ERP database (ADR-017). Independent of hosting: isolation
// rules (TenantId retention, placement thresholds, cutover mechanics) attach to this dimension, while
// ownership rules (credentials, connectivity, migration authority) attach to TenantDatabaseHostingMode.
//
// CustomerManaged implies Dedicated: CustomerManaged + Shared is invalid and is rejected by a database
// CHECK constraint, not merely discouraged.
public enum TenantDatabaseStorageMode
{
  Shared = 1,
  Dedicated = 2
}
