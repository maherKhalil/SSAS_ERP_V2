using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// A physical tenant database as schema health and migration see it (ADR-018): identity, how to reach it,
// who may migrate it, and its current cached health.
//
// No tenant appears here, and that is the point. The migration unit is the physical database — a shared
// database hosting a thousand tenants is one descriptor, checked once and migrated once.
public sealed record TenantDatabaseDescriptor(
  long TenantDatabaseId,
  string ServerKey,
  string DatabaseName,
  TenantDatabaseHostingMode HostingMode,
  TenantDatabaseStorageMode StorageMode,
  TenantDatabaseProvisioningStatus ProvisioningStatus,
  TenantDatabaseMigrationManagementMode MigrationManagementMode,
  TenantDatabaseConnectivityStatus ConnectivityStatus,
  TenantDatabaseSchemaCompatibilityStatus SchemaCompatibilityStatus,
  TenantDatabaseMigrationExecutionStatus MigrationExecutionStatus,
  DateTimeOffset? LastSchemaCheckUtc);
