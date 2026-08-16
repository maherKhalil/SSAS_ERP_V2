using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Trusted tenant -> route resolution (ADR-017).
//
// THIS IS THE AUTHORITATIVE RESOLVER, and it holds no cache. Every call reads current registry state.
//
// It is no longer what consumers receive: TS-Storage Phase E2 registered VersionAwareTenantDatabaseResolver
// against ITenantDatabaseResolver and this type behind it, so a caller gets the version-checked path. That
// separation is deliberate — "which database does the registry say" and "may a remembered answer be reused"
// are different questions, and keeping them in different types is what stops the second from quietly
// answering the first (ADR-020).
//
// Every failure path refuses to route. None of them substitutes another database.
public sealed class TenantDatabaseResolver(ITenantDatabaseRegistryReadRepository repository) : ITenantDatabaseResolver
{
  public async Task<Result<TenantDatabaseRoute>> ResolveAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.TenantContextMissing);
    }

    var assignment = await repository.FindActiveAssignmentAsync(tenantId, cancellationToken);
    if (assignment is null)
    {
      // No active assignment: the tenant is simply not routable. Falling back to the Platform database or
      // to any other tenant database here would cross a placement boundary (ADR-017).
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.ActiveAssignmentMissing);
    }

    // CustomerManaged is architecture-ready in the schema but has no runtime path: no endpoint, no
    // credential reference, no connectivity implementation exists (ADR-021 is implementation-deferred).
    // Rejecting it explicitly is what prevents it being silently treated as platform-managed.
    if (assignment.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.UnsupportedHostingMode);
    }

    // ProvisioningStatus is the only lifecycle dimension that exists yet. The ADR-018 health dimensions
    // (connectivity, schema compatibility, migration execution) are NOT consulted here because nothing
    // maintains them; that gating arrives with the health slice.
    if (assignment.ProvisioningStatus != TenantDatabaseProvisioningStatus.Ready)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.TenantDatabaseNotReady);
    }

    if (string.IsNullOrWhiteSpace(assignment.ServerKey))
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.ServerKeyNotConfigured);
    }

    if (string.IsNullOrWhiteSpace(assignment.DatabaseName) ||
      assignment.DatabaseName.Length > TenantDatabase.DatabaseNameMaximumLength)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.DatabaseNameInvalid);
    }

    // Health travels on the route but is NOT acted on here. The resolver's contract stays "which database";
    // ADR-018 gating is applied on the request path by ITenantDatabaseTrafficGate, because migration
    // tooling must be able to reach exactly the databases that are currently unservable.
    return Result.Success(new TenantDatabaseRoute(
      assignment.TenantId,
      assignment.TenantDatabaseId,
      assignment.ServerKey,
      assignment.DatabaseName,
      assignment.HostingMode,
      assignment.StorageMode,
      assignment.RoutingVersion,
      new TenantDatabaseHealth(
        assignment.ConnectivityStatus,
        assignment.LastConnectivityCheckUtc,
        assignment.SchemaCompatibilityStatus,
        assignment.LastSchemaCheckUtc,
        assignment.MigrationExecutionStatus,
        assignment.MigrationManagementMode,
        assignment.AppliedMigration,
        assignment.TargetMigration)));
  }
}
