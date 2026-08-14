using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// Cached operational health of one PHYSICAL tenant database (ADR-018).
//
// It travels on the route because it comes from the same registry row and answers the question the route
// alone cannot: "may this database serve traffic right now?". It carries no secret — statuses, timestamps
// and migration identifiers only — so the route's secret-containment guarantee is unaffected.
//
// These values are CACHED OBSERVATIONS, never authority. `tenant.__EFMigrationsHistory` is the source of
// truth (ADR-018); a Platform column claiming a version the database does not actually have is the exact
// failure the freshness model below exists to bound.
public sealed record TenantDatabaseHealth(
  TenantDatabaseConnectivityStatus ConnectivityStatus,
  DateTimeOffset? LastConnectivityCheckUtc,
  TenantDatabaseSchemaCompatibilityStatus SchemaCompatibilityStatus,
  DateTimeOffset? LastSchemaCheckUtc,
  TenantDatabaseMigrationExecutionStatus MigrationExecutionStatus,
  TenantDatabaseMigrationManagementMode MigrationManagementMode,
  string? AppliedMigration,
  string? TargetMigration);
