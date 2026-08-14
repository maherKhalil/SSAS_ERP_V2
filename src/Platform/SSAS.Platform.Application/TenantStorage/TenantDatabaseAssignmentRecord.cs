using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// Flat projection of an active assignment joined to its physical database (ADR-017). Carries only the
// registry facts routing needs — no EF entities, and no credential or endpoint material.
public sealed record TenantDatabaseAssignmentRecord(
  Guid TenantId,
  long TenantDatabaseId,
  long RoutingVersion,
  string ServerKey,
  string DatabaseName,
  TenantDatabaseHostingMode HostingMode,
  TenantDatabaseStorageMode StorageMode,
  TenantDatabaseProvisioningStatus ProvisioningStatus,
  // ADR-018 health dimensions, projected from the same physical-database row so routing and gating cost
  // one query between them.
  TenantDatabaseConnectivityStatus ConnectivityStatus,
  DateTimeOffset? LastConnectivityCheckUtc,
  TenantDatabaseSchemaCompatibilityStatus SchemaCompatibilityStatus,
  DateTimeOffset? LastSchemaCheckUtc,
  TenantDatabaseMigrationExecutionStatus MigrationExecutionStatus,
  TenantDatabaseMigrationManagementMode MigrationManagementMode,
  string? AppliedMigration,
  string? TargetMigration);
