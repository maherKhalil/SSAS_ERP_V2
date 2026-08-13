namespace SSAS.Platform.Domain.Enums;

// Who owns and operates the physical SQL Server hosting a tenant ERP database (ADR-017).
// Deliberately independent of TenantDatabaseStorageMode: hosting and isolation are two orthogonal
// properties, and collapsing them into one value is prohibited.
//
// CustomerManaged exists in the data model now for forward compatibility (the CHECK constraint that
// permits it is cheaper to write once than to widen later), but it is NOT runtime-supported: no routing,
// endpoint, credential or connectivity code consumes it until ADR-021 is implemented.
public enum TenantDatabaseHostingMode
{
  PlatformManaged = 1,
  CustomerManaged = 2
}
