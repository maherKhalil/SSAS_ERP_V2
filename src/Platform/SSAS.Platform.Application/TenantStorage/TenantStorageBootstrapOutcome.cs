namespace SSAS.Platform.Application.TenantStorage;

// Result of one tenant-storage bootstrap convergence pass (ADR-017).
public sealed record TenantStorageBootstrapOutcome(
  long TenantDatabaseId,
  bool TenantDatabaseCreated,
  int AssignmentsCreated,
  int TenantsAlreadyAssigned)
{
  public bool ChangedAnything => TenantDatabaseCreated || AssignmentsCreated > 0;
}
